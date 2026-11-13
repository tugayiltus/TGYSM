using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tasks;

namespace Tgysm.Api.IntegrationTests.Tasks;

[Collection(ApiCollection.Name)]
public sealed class TaskActionTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<JsonElement> ActAsync(ApiClient client, JsonElement task, string action, object? extra = null)
    {
        var body = new Dictionary<string, object?> { ["version"] = TaskWorld.VersionOf(task) };
        if (extra is not null)
        {
            foreach (var property in JsonSerializer.SerializeToElement(extra, ApiClient.Json).EnumerateObject())
            {
                body[property.Name] = property.Value;
            }
        }

        using var response = await client.PostAsync(TaskWorld.PathOf(task, action), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Member_claims_starts_and_completes_a_task()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        task = await ActAsync(world.Field, task, "claim");
        Assert.Equal(world.FieldUser.Id, task.GetProperty("assigneeId").GetGuid());
        task = await ActAsync(world.Field, task, "start");
        task = await ActAsync(world.Field, task, "hold", new { reason = "vendor" });
        Assert.Equal("on_hold", task.GetProperty("status").GetString());
        task = await ActAsync(world.Field, task, "resume");
        task = await ActAsync(world.Field, task, "complete", new { resultNote = "Yedek alındı" });

        Assert.Equal("completed", task.GetProperty("status").GetString());
        Assert.Equal("Yedek alındı", task.GetProperty("resultNote").GetString());
        await using var db = factory.Database.CreateDbContext();
        var id = task.GetProperty("id").GetGuid();
        var kinds = await db.TaskEvents.Where(e => e.TaskId == id).OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).Select(e => e.Kind).ToListAsync(Ct);
        Assert.Equal([TaskEventKind.Created, TaskEventKind.Assigned, TaskEventKind.Started, TaskEventKind.Held, TaskEventKind.Resumed, TaskEventKind.Completed], kinds);
    }

    [Fact]
    public async Task Complete_without_result_note_is_refused()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        using var response = await world.Field.PostAsync(TaskWorld.PathOf(task, "complete"), new { version = TaskWorld.VersionOf(task), resultNote = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_member_cannot_act_and_creator_outside_the_team_gets_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        using var response = await world.Desk.PostAsync(TaskWorld.PathOf(task, "start"), new { version = TaskWorld.VersionOf(task) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stale_version_is_a_conflict()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, world.FieldUser.Id);
        await ActAsync(world.Field, task, "start");

        using var response = await world.Field.PostAsync(TaskWorld.PathOf(task, "cancel"), new { version = TaskWorld.VersionOf(task), reason = "Gerek yok" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_moves_the_task_and_mails_the_new_team()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, world.FieldUser.Id);

        task = await ActAsync(world.Field, task, "transfer", new { teamId = world.ServiceDesk.Id, reason = "other_team_responsibility" });

        Assert.Equal(world.ServiceDesk.Id, task.GetProperty("teamId").GetGuid());
        Assert.Equal(JsonValueKind.Null, task.GetProperty("assigneeId").ValueKind);
        Assert.False(task.GetProperty("canWork").GetBoolean());
        await using var db = factory.Database.CreateDbContext();
        var id = task.GetProperty("id").GetGuid();
        Assert.Equal(2, await db.OutboxEvents.CountAsync(e => e.EventType == NotificationEventTypes.TaskAssigned && e.SubjectId == id, Ct));
    }

    [Fact]
    public async Task Transfer_with_reason_other_needs_a_note()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        using var response = await world.Field.PostAsync(TaskWorld.PathOf(task, "transfer"), new { version = TaskWorld.VersionOf(task), teamId = world.ServiceDesk.Id, reason = "other" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Assign_to_a_non_member_is_refused()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        using var response = await world.Field.PostAsync(TaskWorld.PathOf(task, "assign"), new { version = TaskWorld.VersionOf(task), assigneeId = world.DeskUser.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("tasks.assignee_not_in_team", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Cancel_and_reopen()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        task = await ActAsync(world.Field, task, "cancel", new { reason = "Gerek kalmadı" });
        Assert.Equal("cancelled", task.GetProperty("status").GetString());
        task = await ActAsync(world.Field, task, "reopen");

        Assert.Equal("open", task.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, task.GetProperty("cancelReason").ValueKind);
    }

    [Fact]
    public async Task Comment_is_recorded_in_history()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        using var response = await world.Field.PostAsync(TaskWorld.PathOf(task, "comments"), new { body = "Yedek 02:00'de alınacak" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        var id = task.GetProperty("id").GetGuid();
        Assert.True(await db.TaskEvents.AnyAsync(e => e.TaskId == id && e.Kind == TaskEventKind.Commented, Ct));
    }

    [Fact]
    public async Task Timeline_lists_history_and_comments_in_order_with_the_actor()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);
        await ActAsync(world.Field, task, "claim");
        using (var comment = await world.Field.PostAsync(TaskWorld.PathOf(task, "comments"), new { body = " Yedek 02:00'de alınacak " }))
        {
            Assert.Equal(HttpStatusCode.NoContent, comment.StatusCode);
        }

        using var response = await world.Desk.GetAsync(TaskWorld.PathOf(task, "timeline"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await world.Field.ReadJsonAsync<JsonElement>(response)).EnumerateArray().ToList();
        Assert.Equal(["created", "assigned", "commented"], items.Select(i => i.GetProperty("kind").GetString()));
        Assert.Equal("Yedek 02:00'de alınacak", items[2].GetProperty("data").GetProperty("body").GetString());
        Assert.False(string.IsNullOrWhiteSpace(items[2].GetProperty("actorName").GetString()));
    }

    [Fact]
    public async Task Employee_gets_404_for_the_timeline()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        using var response = await world.Employee.GetAsync(TaskWorld.PathOf(task, "timeline"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Task_actions_leave_the_linked_incident_and_its_sla_unchanged()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var ticketId = incident.GetProperty("id").GetGuid();
        await using (var before = factory.Database.CreateDbContext())
        {
            Assert.True(await before.SlaInstances.AnyAsync(i => i.TicketId == ticketId, Ct));
        }

        var slaBefore = await SlaSnapshotAsync(ticketId);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, ticketNumber: TicketWorld.NumberOf(incident));
        task = await ActAsync(world.Field, task, "claim");
        task = await ActAsync(world.Field, task, "start");
        task = await ActAsync(world.Field, task, "complete", new { resultNote = "Bitti" });
        task = await ActAsync(world.Field, task, "reopen");
        await ActAsync(world.Field, task, "cancel", new { reason = "Vazgeçildi" });

        var after = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(incident));
        Assert.Equal(TicketWorld.VersionOf(incident), TicketWorld.VersionOf(after));
        Assert.Equal("open", after.GetProperty("status").GetString());
        Assert.Equal(slaBefore, await SlaSnapshotAsync(ticketId));
    }

    private async Task<string> SlaSnapshotAsync(Guid ticketId)
    {
        await using var db = factory.Database.CreateDbContext();
        var rows = await db.SlaInstances.AsNoTracking().Where(i => i.TicketId == ticketId).OrderBy(i => i.Id).ToListAsync(Ct);
        return JsonSerializer.Serialize(rows.Select(i => new { i.Id, i.Version, i.StoppedAt, i.WarnedAt, i.BreachedAt }));
    }
}
