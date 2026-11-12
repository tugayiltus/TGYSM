using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;

namespace Tgysm.Api.IntegrationTests.Tasks;

[Collection(ApiCollection.Name)]
public sealed class TaskScopeTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Other_team_sees_the_task_but_not_the_linked_incident()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, ticketNumber: TicketWorld.NumberOf(incident));

        var seen = await TicketWorld.ReadAsync(world.Field, TaskWorld.PathOf(task));
        using var incidentForField = await world.Field.GetAsync(TicketWorld.PathOf(incident));

        var link = seen.GetProperty("link");
        Assert.False(link.GetProperty("accessible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, link.GetProperty("number").ValueKind);
        Assert.Equal(JsonValueKind.Null, link.GetProperty("title").ValueKind);
        Assert.True(seen.GetProperty("canWork").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, incidentForField.StatusCode);
    }

    [Fact]
    public async Task Creator_with_access_sees_the_link()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, ticketNumber: TicketWorld.NumberOf(incident));

        var seen = await TicketWorld.ReadAsync(world.Desk, TaskWorld.PathOf(task));

        var link = seen.GetProperty("link");
        Assert.True(link.GetProperty("accessible").GetBoolean());
        Assert.Equal(TicketWorld.NumberOf(incident), link.GetProperty("number").GetString());
        Assert.False(seen.GetProperty("canWork").GetBoolean());
    }

    [Fact]
    public async Task Employee_gets_404_for_any_task()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        using var read = await world.Employee.GetAsync(TaskWorld.PathOf(task));
        using var create = await world.Employee.PostAsync(TaskWorld.TasksPath, new { title = "x", teamId = world.FieldTeam.Id, priority = "p3" });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    [Fact]
    public async Task Unrelated_it_staff_gets_404_and_admin_sees_all()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id);

        using var desk = await world.Desk.GetAsync(TaskWorld.PathOf(task));
        using var missing = await world.Field.GetAsync($"{TaskWorld.TasksPath}/TSK-2026-999999");
        var admin = await TicketWorld.ReadAsync(world.Admin, TaskWorld.PathOf(task));

        Assert.Equal(HttpStatusCode.NotFound, desk.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.True(admin.GetProperty("canWork").GetBoolean());
    }

    [Fact]
    public async Task Assignee_outside_the_team_still_sees_the_task()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id, world.FieldUser.Id);
        await TestData.SaveAsync(factory, db => db.TeamMemberships.RemoveRange(db.TeamMemberships.Where(m => m.TeamId == world.FieldTeam.Id && m.UserId == world.FieldUser.Id)));

        var seen = await TicketWorld.ReadAsync(world.Field, TaskWorld.PathOf(task));

        Assert.False(seen.GetProperty("canWork").GetBoolean());
    }
}
