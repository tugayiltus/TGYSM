using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeCreationTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Guid> AssetAsync(TicketWorld world, string tag) => await TestData.LegacyAssetAsync(factory, tag, world.Office.Id);

    [Fact]
    public async Task Create_gives_a_numbered_draft_with_history_audit_and_assets()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await AssetAsync(world, "SRV-1");

        var created = await ChangeWorld.CreateAsync(world.Desk, world, assetIds: [asset, asset]);

        var detail = (await TicketWorld.ReadAsync(world.Desk, ChangeWorld.PathOf(created))).GetProperty("detail");
        Assert.Matches("^CHG-[0-9]{4}-000001$", ChangeWorld.NumberOf(created));
        Assert.Equal("draft", detail.GetProperty("status").GetString());
        Assert.Equal("Servis Masası", detail.GetProperty("ownerTeamName").GetString());
        Assert.Equal(asset, Assert.Single(detail.GetProperty("assets").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.True(detail.GetProperty("hasEligibleApprover").GetBoolean());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("approval").ValueKind);
        await using var db = factory.Database.CreateDbContext();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(ChangeEventKind.Created, (await db.ChangeEvents.SingleAsync(e => e.ChangeId == id, TestContext.Current.CancellationToken)).Kind);
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.ChangeCreated && a.EntityId == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_by_a_non_member_of_the_owner_team_is_404_but_admin_may()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var body = new { draft = ChangeWorld.DraftBody(), ownerTeamId = world.ServiceDesk.Id, ownerId = world.DeskUser.Id, assetIds = Array.Empty<Guid>() };

        using var field = await world.Field.PostAsync(ChangeWorld.ChangesPath, body);
        using var admin = await world.Admin.PostAsync(ChangeWorld.ChangesPath, body);

        Assert.Equal(HttpStatusCode.NotFound, field.StatusCode);
        Assert.Equal(HttpStatusCode.Created, admin.StatusCode);
    }

    [Fact]
    public async Task Owner_outside_the_owner_team_is_409()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Desk.PostAsync(ChangeWorld.ChangesPath, new
        {
            draft = ChangeWorld.DraftBody(),
            ownerTeamId = world.ServiceDesk.Id,
            ownerId = world.FieldUser.Id,
            assetIds = Array.Empty<Guid>(),
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.owner_not_in_team", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Unknown_asset_and_empty_title_are_400()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var asset = await world.Desk.PostAsync(ChangeWorld.ChangesPath, new
        {
            draft = ChangeWorld.DraftBody(),
            ownerTeamId = world.ServiceDesk.Id,
            ownerId = world.DeskUser.Id,
            assetIds = new[] { Guid.NewGuid() },
        });
        using var title = await world.Desk.PostAsync(ChangeWorld.ChangesPath, new
        {
            draft = ChangeWorld.DraftBody(title: " "),
            ownerTeamId = world.ServiceDesk.Id,
            ownerId = world.DeskUser.Id,
            assetIds = Array.Empty<Guid>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, asset.StatusCode);
        Assert.Contains("assetIds", (await ApiClient.ReadFieldCodesAsync(asset)).Keys);
        Assert.Equal(HttpStatusCode.BadRequest, title.StatusCode);
        Assert.Contains("title", (await ApiClient.ReadFieldCodesAsync(title)).Keys);
    }

    [Fact]
    public async Task List_queues_and_search_narrow_the_scope()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var first = await ChangeWorld.CreateAsync(world.Desk, world, ChangeWorld.DraftBody("Yazıcı sunucusu"));
        await ChangeWorld.CreateAsync(world.Admin, world, ChangeWorld.DraftBody("Ağ anahtarı"));

        var mine = await TicketWorld.ReadAsync(world.Desk, $"{ChangeWorld.ChangesPath}?queue=mine");
        var team = await TicketWorld.ReadAsync(world.Desk, $"{ChangeWorld.ChangesPath}?queue=team&search=yazici");
        var byNumber = await TicketWorld.ReadAsync(world.Admin, $"{ChangeWorld.ChangesPath}?search={ChangeWorld.NumberOf(first)}");
        var pending = await TicketWorld.ReadAsync(world.Admin, $"{ChangeWorld.ChangesPath}?queue=pending_approval");
        var open = await TicketWorld.ReadAsync(world.Admin, $"{ChangeWorld.ChangesPath}?queue=open&risk=medium");

        Assert.Equal(2, mine.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, team.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, byNumber.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, pending.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, open.GetProperty("totalCount").GetInt32());
    }
}
