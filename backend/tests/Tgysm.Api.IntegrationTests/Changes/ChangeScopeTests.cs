using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeScopeTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Employee_gets_404_for_list_get_and_create()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var list = await world.Employee.GetAsync(ChangeWorld.ChangesPath);
        using var read = await world.Employee.GetAsync(ChangeWorld.PathOf(change));
        using var create = await world.Employee.PostAsync(ChangeWorld.ChangesPath, new
        {
            draft = ChangeWorld.DraftBody(),
            ownerTeamId = world.ServiceDesk.Id,
            ownerId = world.DeskUser.Id,
            assetIds = Array.Empty<Guid>(),
        });

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    [Fact]
    public async Task Owner_team_member_and_admin_see_the_full_detail()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        var desk = await TicketWorld.ReadAsync(world.Desk, ChangeWorld.PathOf(change));
        var admin = await TicketWorld.ReadAsync(world.Admin, ChangeWorld.PathOf(change));

        Assert.Equal("full", desk.GetProperty("access").GetString());
        Assert.Equal("full", admin.GetProperty("access").GetString());
        Assert.True(desk.GetProperty("detail").GetProperty("canWork").GetBoolean());
        Assert.Equal(4, admin.GetProperty("detail").GetProperty("tasksByPhase").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, admin.GetProperty("summary").ValueKind);
    }

    [Fact]
    public async Task Backout_only_member_gets_a_summary_with_the_backout_plan()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Backout, world.FieldTeam.Id);

        var seen = await TicketWorld.ReadAsync(world.Field, ChangeWorld.PathOf(change));
        using var timeline = await world.Field.GetAsync(ChangeWorld.PathOf(change, "timeline"));
        using var edit = await world.Field.PutAsync(ChangeWorld.PathOf(change), new
        {
            version = ChangeWorld.VersionOf(change),
            draft = ChangeWorld.DraftBody("Yeni"),
            assetIds = Array.Empty<Guid>(),
        });

        Assert.Equal("summary", seen.GetProperty("access").GetString());
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("detail").ValueKind);
        var summary = seen.GetProperty("summary");
        Assert.Equal(ChangeWorld.BackoutPlan, summary.GetProperty("backoutPlan").GetString());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("implementationPlan").ValueKind);
        Assert.Equal("medium", summary.GetProperty("risk").GetString());
        Assert.False(summary.TryGetProperty("approval", out _));
        Assert.False(summary.TryGetProperty("assets", out _));
        Assert.Equal(HttpStatusCode.NotFound, timeline.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
    }

    [Fact]
    public async Task Assignee_of_an_implementation_phase_task_sees_the_implementation_plan_only()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id, world.FieldUser.Id);

        var summary = (await TicketWorld.ReadAsync(world.Field, ChangeWorld.PathOf(change))).GetProperty("summary");

        Assert.Equal(ChangeWorld.ImplementationPlan, summary.GetProperty("implementationPlan").GetString());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("backoutPlan").ValueKind);
    }

    [Fact]
    public async Task Unrelated_it_staff_gets_404_and_list_shows_only_visible_changes()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var read = await world.Field.GetAsync(ChangeWorld.PathOf(change));
        using var missing = await world.Field.GetAsync($"{ChangeWorld.ChangesPath}/CHG-2026-999999");
        var fieldList = await TicketWorld.ReadAsync(world.Field, ChangeWorld.ChangesPath);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Backout, world.FieldTeam.Id);
        var afterTask = await TicketWorld.ReadAsync(world.Field, ChangeWorld.ChangesPath);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(0, fieldList.GetProperty("totalCount").GetInt32());
        var row = Assert.Single(afterTask.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("ownerTeamName").ValueKind);
    }

    [Fact]
    public async Task Summary_list_row_hides_the_outcome_and_owner_names()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Backout, world.FieldTeam.Id);
        var id = change.GetProperty("id").GetGuid();
        await TestData.SaveAsync(factory, db => db.Entry(db.ChangeRequests.Single(c => c.Id == id)).Property(c => c.Outcome).CurrentValue = ChangeOutcome.Successful);

        var field = Assert.Single((await TicketWorld.ReadAsync(world.Field, ChangeWorld.ChangesPath)).GetProperty("items").EnumerateArray());
        var desk = Assert.Single((await TicketWorld.ReadAsync(world.Desk, ChangeWorld.ChangesPath)).GetProperty("items").EnumerateArray());

        Assert.Equal(JsonValueKind.Null, field.GetProperty("outcome").ValueKind);
        Assert.Equal(JsonValueKind.Null, field.GetProperty("ownerName").ValueKind);
        Assert.Equal(JsonValueKind.Null, field.GetProperty("ownerTeamName").ValueKind);
        Assert.Equal("successful", desk.GetProperty("outcome").GetString());
    }
}
