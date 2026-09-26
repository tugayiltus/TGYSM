using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Assets;

[Collection(ApiCollection.Name)]
public sealed class AssetCatalogueTests(ApiFactory factory) : IAsyncLifetime
{
    public const string AssetsPath = "/api/v1/assets";
    private Guid _storeId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _storeId = (await TestData.LocationAsync(factory, "Depo")).Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> ItAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com")).Client;

    private object NewAsset(string tag, string? serial = null, string model = "T14", string type = "laptop") =>
        new { assetTag = tag, type, brand = "Lenovo", model, serialNumber = serial, locationId = _storeId };

    public static async Task<JsonElement> CreateAsync(ApiClient client, object body)
    {
        using var response = await client.PostAsync(AssetsPath, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Employee_cannot_use_the_inventory()
    {
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "e@example.com");

        using var list = await employee.GetAsync(AssetsPath);
        using var create = await employee.PostAsync(AssetsPath, NewAsset("DT-1"));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task It_staff_creates_lists_filters_and_reads_assets()
    {
        var it = await ItAsync();
        var created = await CreateAsync(it, NewAsset("dt 0001", "sn-1"));
        await CreateAsync(it, NewAsset("TB-1", model: "iPad", type: "tablet"));

        using var byType = await it.GetAsync($"{AssetsPath}?type=tablet");
        using var bySearch = await it.GetAsync($"{AssetsPath}?search=DT0001");
        using var detail = await it.GetAsync($"{AssetsPath}/{created.GetProperty("id").GetGuid()}");

        Assert.Equal("in_stock", created.GetProperty("status").GetString());
        Assert.Equal("Depo", created.GetProperty("locationName").GetString());
        Assert.Equal(1, (await it.ReadJsonAsync<JsonElement>(byType)).GetProperty("totalCount").GetInt32());
        Assert.Equal("dt 0001", (await it.ReadJsonAsync<JsonElement>(bySearch)).GetProperty("items")[0].GetProperty("assetTag").GetString());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.AssetCreated, Ct));
        Assert.Equal(2, await db.AssetMovements.CountAsync(Ct));
    }

    [Fact]
    public async Task Duplicate_tag_or_serial_is_refused()
    {
        var it = await ItAsync();
        await CreateAsync(it, NewAsset("DT-1", "SN-1"));

        using var tag = await it.PostAsync(AssetsPath, NewAsset(" dt-1 "));
        using var serial = await it.PostAsync(AssetsPath, NewAsset("DT-2", "sn-1"));

        Assert.Equal(ErrorCodes.AssetTagTaken, await ApiClient.ReadCodeAsync(tag));
        Assert.Equal(ErrorCodes.AssetSerialTaken, await ApiClient.ReadCodeAsync(serial));
    }

    [Fact]
    public async Task Inactive_location_is_refused()
    {
        var it = await ItAsync();
        var closed = await TestData.LocationAsync(factory, "Kapalı", active: false);

        using var response = await it.PostAsync(AssetsPath, new { assetTag = "DT-9", type = "laptop", brand = "Lenovo", model = "T14", locationId = closed.Id });

        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Update_changes_the_card_and_stale_version_is_a_conflict()
    {
        var it = await ItAsync();
        var created = await CreateAsync(it, NewAsset("DT-1"));
        var path = $"{AssetsPath}/{created.GetProperty("id").GetGuid()}";
        var version = created.GetProperty("version").GetUInt32();
        object Body(string model) => new { version, assetTag = "DT-1", type = "laptop", brand = "Lenovo", model, locationId = _storeId, notes = "Yedek şarj aleti var" };

        using var first = await it.PutAsync(path, Body("T14 Gen 5"));
        using var second = await it.PutAsync(path, Body("T14 Gen 6"));

        Assert.Equal("T14 Gen 5", (await it.ReadJsonAsync<JsonElement>(first)).GetProperty("model").GetString());
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task Validation_reports_fields()
    {
        var it = await ItAsync();

        using var response = await it.PostAsync(AssetsPath, new { assetTag = "", type = "toaster", brand = "", model = "", locationId = _storeId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
