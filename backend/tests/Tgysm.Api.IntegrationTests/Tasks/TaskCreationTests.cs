using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tasks;

namespace Tgysm.Api.IntegrationTests.Tasks;

[Collection(ApiCollection.Name)]
public sealed class TaskCreationTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Standalone_task_gets_a_number_history_audit_and_assignee_mail()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, world.FieldUser.Id, dueDate: "2026-12-01");

        Assert.Matches("^TSK-[0-9]{4}-000001$", TaskWorld.NumberOf(task));
        Assert.Equal("open", task.GetProperty("status").GetString());
        Assert.Equal("Saha Destek", task.GetProperty("teamName").GetString());
        Assert.Equal("2026-12-01", task.GetProperty("dueDate").GetString());
        Assert.Equal(JsonValueKind.Null, task.GetProperty("link").ValueKind);
        await using var db = factory.Database.CreateDbContext();
        var id = task.GetProperty("id").GetGuid();
        Assert.Equal(TaskEventKind.Created, (await db.TaskEvents.SingleAsync(e => e.TaskId == id, TestContext.Current.CancellationToken)).Kind);
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.TaskCreated && a.EntityId == id, TestContext.Current.CancellationToken));
        var mail = await db.OutboxEvents.SingleAsync(e => e.EventType == NotificationEventTypes.TaskAssigned, TestContext.Current.CancellationToken);
        Assert.Equal((OutboxSubjectTypes.Task, (Guid?)id, (Guid?)world.FieldUser.Id), (mail.SubjectType, mail.SubjectId, mail.UserId));
    }

    [Fact]
    public async Task Task_without_assignee_sends_no_assignee_mail()
    {
        var world = await TicketWorld.CreateAsync(factory);

        await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.EventType == NotificationEventTypes.TaskAssigned, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Assignee_outside_the_team_is_refused()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Desk.PostAsync(TaskWorld.TasksPath, new { title = "Yedek", teamId = world.FieldTeam.Id, assigneeId = world.DeskUser.Id, priority = "p2" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("tasks.assignee_not_in_team", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Ticket_link_needs_work_access_to_the_ticket()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var response = await world.Field.PostAsync(TaskWorld.TasksPath, new { title = "Yedek", teamId = world.FieldTeam.Id, priority = "p2", ticketNumber = TicketWorld.NumberOf(incident) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Missing_title_is_a_validation_error()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Desk.PostAsync(TaskWorld.TasksPath, new { title = " ", teamId = world.FieldTeam.Id, priority = "p2" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_task_leaves_the_linked_ticket_untouched()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, ticketNumber: TicketWorld.NumberOf(incident));
        var after = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(incident));

        Assert.Equal(TicketWorld.VersionOf(incident), TicketWorld.VersionOf(after));
    }
}
