using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Features.Assets;

namespace Tgysm.Api.IntegrationTests.Assets;

[Collection(ApiCollection.Name)]
public sealed class AssetLabelTests(ApiFactory factory) : IAsyncLifetime
{
    private const string LabelsPath = "/api/v1/assets/labels";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object Body(IEnumerable<Guid> ids) => new { ids = ids.ToArray() };

    private async Task<JsonElement> CreateAssetAsync(TicketWorld world, string? serial)
    {
        var store = await TestData.LocationAsync(factory, $"Depo {Guid.NewGuid():N}");
        return await AssetCatalogueTests.CreateAsync(world.Desk, new { modelId = await TestData.ModelAsync(factory), locationId = store.Id, serialNumber = serial });
    }

    [Fact]
    public async Task Labels_come_in_request_order_with_the_public_address_and_skip_unknown_ids()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var first = await CreateAssetAsync(world, "SN-1");
        var second = await CreateAssetAsync(world, serial: null);
        Guid[] ids = [second.GetProperty("id").GetGuid(), Guid.CreateVersion7(), first.GetProperty("id").GetGuid()];

        using var response = await world.Desk.PostAsync(LabelsPath, Body(ids));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await world.Desk.ReadJsonAsync<JsonElement>(response);
        Assert.Equal(ApiFactory.PublicBaseUrl, body.GetProperty("publicBaseUrl").GetString());
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([ids[0], ids[2]], items.Select(i => i.GetProperty("id").GetGuid()));
        Assert.Equal(first.GetProperty("assetTag").GetString(), items[1].GetProperty("assetTag").GetString());
        Assert.Equal("SN-1", items[1].GetProperty("serialNumber").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("serialNumber").ValueKind);
        Assert.False(string.IsNullOrEmpty(items[0].GetProperty("brand").GetString()));
        Assert.False(string.IsNullOrEmpty(items[0].GetProperty("model").GetString()));
    }

    [Fact]
    public async Task More_labels_than_the_limit_are_refused()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Desk.PostAsync(LabelsPath, Body(Enumerable.Range(0, GetAssetLabelsHandler.MaxLabelsPerRequest + 1).Select(_ => Guid.CreateVersion7())));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Employees_cannot_read_labels()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Employee.PostAsync(LabelsPath, Body([Guid.CreateVersion7()]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
