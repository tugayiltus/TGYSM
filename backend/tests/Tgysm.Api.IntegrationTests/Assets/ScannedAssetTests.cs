using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;

namespace Tgysm.Api.IntegrationTests.Assets;

[Collection(ApiCollection.Name)]
public sealed class ScannedAssetTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ScanPath = "/api/v1/scan/assets";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<JsonElement> CreateAssetAsync(TicketWorld world)
    {
        var store = await TestData.LocationAsync(factory, $"Depo {Guid.NewGuid():N}");
        return await AssetCatalogueTests.CreateAsync(world.Desk, new { modelId = await TestData.ModelAsync(factory), locationId = store.Id });
    }

    [Fact]
    public async Task An_employee_reads_only_the_tag_type_brand_model_and_whether_it_can_be_reported()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await CreateAssetAsync(world);

        using var response = await world.Employee.GetAsync($"{ScanPath}/{asset.GetProperty("id").GetGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await world.Employee.ReadJsonAsync<JsonElement>(response);
        Assert.Equal(["id", "assetTag", "type", "brand", "model", "canReport"], body.EnumerateObject().Select(p => p.Name));
        Assert.Equal(asset.GetProperty("assetTag").GetString(), body.GetProperty("assetTag").GetString());
        Assert.True(body.GetProperty("canReport").GetBoolean());
    }

    [Fact]
    public async Task A_retired_device_cannot_be_reported()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await CreateAssetAsync(world);
        var id = asset.GetProperty("id").GetGuid();
        using var retire = await world.Desk.PostAsync($"{AssetCatalogueTests.AssetsPath}/{id}/retire", new { version = asset.GetProperty("version").GetUInt32(), reason = "Eski" });
        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);

        using var response = await world.Employee.GetAsync($"{ScanPath}/{id}");

        Assert.False((await world.Employee.ReadJsonAsync<JsonElement>(response)).GetProperty("canReport").GetBoolean());
    }

    [Fact]
    public async Task An_unknown_device_is_not_found()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Employee.GetAsync($"{ScanPath}/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_visitor_without_a_session_is_refused()
    {
        var anonymous = ApiClient.Create(factory);

        using var response = await anonymous.GetAsync($"{ScanPath}/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
