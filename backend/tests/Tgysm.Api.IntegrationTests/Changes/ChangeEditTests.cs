using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeEditTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object EditBody(JsonElement change, object draft, IEnumerable<Guid> assetIds) =>
        new { version = ChangeWorld.VersionOf(change), draft, assetIds };

    [Fact]
    public async Task Edit_in_draft_changes_fields_and_assets_and_writes_history()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var oldAsset = await TestData.LegacyAssetAsync(factory, "SRV-1", world.Office.Id);
        var newAsset = await TestData.LegacyAssetAsync(factory, "SRV-2", world.Office.Id);
        var change = await ChangeWorld.CreateAsync(world.Desk, world, assetIds: [oldAsset]);

        using var response = await world.Desk.PutAsync(ChangeWorld.PathOf(change), EditBody(change, ChangeWorld.DraftBody("Yeni başlık"), [newAsset]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await world.Desk.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("Yeni başlık", edited.GetProperty("title").GetString());
        Assert.Equal(newAsset, Assert.Single(edited.GetProperty("assets").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.NotEqual(ChangeWorld.VersionOf(change), edited.GetProperty("version").GetUInt32());
        var timeline = await TicketWorld.ReadAsync(world.Desk, ChangeWorld.PathOf(change, "timeline"));
        var kinds = timeline.EnumerateArray().Select(e => e.GetProperty("kind").GetString()).ToList();
        Assert.Equal(["created", "updated", "assets_changed"], kinds);
        var updated = timeline.EnumerateArray().ElementAt(1).GetProperty("data").GetProperty("fields");
        Assert.Equal("Title", Assert.Single(updated.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Stale_version_is_409()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        using var first = await world.Desk.PutAsync(ChangeWorld.PathOf(change), EditBody(change, ChangeWorld.DraftBody("A"), []));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var stale = await world.Desk.PutAsync(ChangeWorld.PathOf(change), EditBody(change, ChangeWorld.DraftBody("B"), []));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await ApiClient.ReadCodeAsync(stale));
    }

    [Fact]
    public async Task Edit_outside_draft_is_refused()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var id = change.GetProperty("id").GetGuid();
        await TestData.SaveAsync(factory, db => db.ChangeRequests.Single(c => c.Id == id).Cancel("Vazgeçildi", DateTimeOffset.UtcNow));
        var fresh = await TicketWorld.ReadAsync(world.Desk, ChangeWorld.PathOf(change));

        using var response = await world.Desk.PutAsync(ChangeWorld.PathOf(change), EditBody(fresh.GetProperty("detail"), ChangeWorld.DraftBody("X"), []));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.invalid_transition", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Edit_by_unrelated_staff_is_404_and_unknown_asset_is_400()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var field = await world.Field.PutAsync(ChangeWorld.PathOf(change), EditBody(change, ChangeWorld.DraftBody("X"), []));
        using var unknown = await world.Desk.PutAsync(ChangeWorld.PathOf(change), EditBody(change, ChangeWorld.DraftBody("X"), [Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.NotFound, field.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal("Sunucu güncellemesi", (await db.ChangeRequests.SingleAsync(TestContext.Current.CancellationToken)).Title);
    }

    [Fact]
    public async Task Non_member_with_an_unknown_asset_id_gets_404_not_400()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var response = await world.Field.PutAsync(ChangeWorld.PathOf(change), EditBody(change, ChangeWorld.DraftBody("X"), [Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
