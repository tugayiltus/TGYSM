using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Assets;

[Collection(ApiCollection.Name)]
public sealed class AssetLifecycleTests(ApiFactory factory) : IAsyncLifetime
{
    private const string AssetsPath = AssetCatalogueTests.AssetsPath;
    private const string MyAssetsPath = "/api/v1/me/assets";
    private const int ParallelHandovers = 4;
    private Guid _storeId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _storeId = (await TestData.LocationAsync(factory, "Depo")).Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<(ApiClient It, JsonElement Asset)> ItWithAssetAsync()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        var asset = await AssetCatalogueTests.CreateAsync(it, new { assetTag = "DT-1", modelId = await TestData.ModelAsync(factory), locationId = _storeId });
        return (it, asset);
    }

    private static string PathOf(JsonElement asset, string operation) => $"{AssetsPath}/{asset.GetProperty("id").GetGuid()}/{operation}";

    private static uint VersionOf(JsonElement asset) => asset.GetProperty("version").GetUInt32();

    private static async Task<JsonElement> OkAsync(ApiClient client, HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await client.ReadJsonAsync<JsonElement>(response);
        }
    }

    [Fact]
    public async Task Full_lifecycle_writes_movements_and_audit()
    {
        var (it, asset) = await ItWithAssetAsync();
        var first = await TestUsers.CreateAsync(factory, UserRole.Employee, "first@example.com", firstName: "Ayşe", lastName: "Kaya");
        var second = await TestUsers.CreateAsync(factory, UserRole.Employee, "second@example.com");

        asset = await OkAsync(it, await it.PostAsync(PathOf(asset, "assign"), new { version = VersionOf(asset), userId = first.Id, note = "yeni" }));
        Assert.Equal("assigned", asset.GetProperty("status").GetString());
        Assert.Equal("Ayşe Kaya", asset.GetProperty("assignedUserName").GetString());

        asset = await OkAsync(it, await it.PostAsync(PathOf(asset, "transfer"), new { version = VersionOf(asset), userId = second.Id }));
        Assert.Equal(second.Id, asset.GetProperty("assignedUserId").GetGuid());

        asset = await OkAsync(it, await it.PostAsync(PathOf(asset, "maintenance/start"), new { version = VersionOf(asset), retainResponsibility = true }));
        asset = await OkAsync(it, await it.PostAsync(PathOf(asset, "maintenance/end"), new { version = VersionOf(asset) }));
        Assert.Equal("assigned", asset.GetProperty("status").GetString());

        asset = await OkAsync(it, await it.PostAsync(PathOf(asset, "return"), new { version = VersionOf(asset), locationId = _storeId }));
        asset = await OkAsync(it, await it.PostAsync(PathOf(asset, "lost"), new { version = VersionOf(asset), reason = "Kargoda kayboldu" }));
        asset = await OkAsync(it, await it.PostAsync(PathOf(asset, "found"), new { version = VersionOf(asset), locationId = _storeId }));
        asset = await OkAsync(it, await it.PostAsync(PathOf(asset, "retire"), new { version = VersionOf(asset), reason = "Ekran kırık" }));
        Assert.Equal("retired", asset.GetProperty("status").GetString());

        using var history = await it.GetAsync($"{AssetsPath}/{asset.GetProperty("id").GetGuid()}/history");
        Assert.Equal(9, (await it.ReadJsonAsync<JsonElement>(history)).GetProperty("movements").GetArrayLength());
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(2, await db.AssetAssignments.CountAsync(Ct));
        Assert.Equal(0, await db.AssetAssignments.CountAsync(a => a.ReturnedAt == null, Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.AssetRetired, Ct));
    }

    [Fact]
    public async Task Invalid_transition_and_inactive_user_are_refused()
    {
        var (it, asset) = await ItWithAssetAsync();
        var gone = await TestUsers.CreateAsync(factory, UserRole.Employee, "gone@example.com");
        await TestData.SaveAsync(factory, db => db.Users.Attach(gone).Entity.Deactivate(DateTimeOffset.UtcNow));

        using var toInactive = await it.PostAsync(PathOf(asset, "assign"), new { version = VersionOf(asset), userId = gone.Id });
        using var endMaintenance = await it.PostAsync(PathOf(asset, "maintenance/end"), new { version = VersionOf(asset) });
        using var noReason = await it.PostAsync(PathOf(asset, "retire"), new { version = VersionOf(asset), reason = "" });

        Assert.Equal(ErrorCodes.AssetUserInactive, await ApiClient.ReadCodeAsync(toInactive));
        Assert.Equal(ErrorCodes.AssetInvalidTransition, await ApiClient.ReadCodeAsync(endMaintenance));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
    }

    [Fact]
    public async Task My_assets_lists_only_my_active_assignments()
    {
        var (it, asset) = await ItWithAssetAsync();
        var (employee, me) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "me@example.com");
        var other = await AssetCatalogueTests.CreateAsync(it, new { assetTag = "DT-2", modelId = await TestData.ModelAsync(factory, "Apple", "iPhone", Domain.Assets.AssetType.Phone), locationId = _storeId });
        await OkAsync(it, await it.PostAsync(PathOf(asset, "assign"), new { version = VersionOf(asset), userId = me.Id }));

        using var mine = await employee.GetAsync(MyAssetsPath);
        using var inventory = await employee.GetAsync(AssetsPath);

        var items = await employee.ReadJsonAsync<JsonElement>(mine);
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(asset.GetProperty("assetTag").GetString(), items[0].GetProperty("assetTag").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, inventory.StatusCode);
        Assert.NotEqual(Guid.Empty, other.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Parallel_handovers_create_exactly_one_active_assignment()
    {
        var (it, asset) = await ItWithAssetAsync();
        var users = new List<User>();
        for (var i = 0; i < ParallelHandovers; i++)
        {
            users.Add(await TestUsers.CreateAsync(factory, UserRole.Employee, $"u{i}@example.com"));
        }

        await it.RefreshCsrfAsync();
        var responses = await Task.WhenAll(users.Select(u => it.PostAsync(PathOf(asset, "assign"), new { version = VersionOf(asset), userId = u.Id })));
        var statuses = responses.Select(r => r.StatusCode).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.All(statuses.Where(s => s != HttpStatusCode.OK), s => Assert.Equal(HttpStatusCode.Conflict, s));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.AssetAssignments.CountAsync(a => a.ReturnedAt == null, Ct));
    }
}
