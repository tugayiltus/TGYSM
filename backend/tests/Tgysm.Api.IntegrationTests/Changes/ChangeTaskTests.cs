using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tasks;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeTaskTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object TaskBody(Guid teamId, string? changeNumber, string? phase, string? ticketNumber = null) =>
        new { title = "Değişiklik görevi", teamId, priority = "p2", changeNumber, phase, ticketNumber };

    private async Task<List<ChangeApproval>> ApprovalsAsync(JsonElement change)
    {
        var id = change.GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ChangeApprovals.AsNoTracking().Where(a => a.ChangeId == id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync(Ct);
    }

    private static async Task<string?> StartCodeAsync(ApiClient client, JsonElement task)
    {
        using var response = await client.PostAsync(TaskWorld.PathOf(task, "start"), new { version = TaskWorld.VersionOf(task) });
        return response.StatusCode == HttpStatusCode.OK ? null : await ApiClient.ReadCodeAsync(response);
    }

    [Fact]
    public async Task Implementation_task_cannot_start_while_scheduled_but_a_preparation_task_can()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, implementation) = await ChangeWorld.ScheduledAsync(world);
        var preparation = await ChangeWorld.CreateTaskAsync(world.Desk, scheduled, "preparation", world.ServiceDesk.Id, world.DeskUser.Id);

        Assert.Equal("changes.phase_not_active", await StartCodeAsync(world.Desk, implementation));
        var started = await ChangeWorld.TaskActAsync(world.Desk, preparation, "start");
        Assert.Equal("in_progress", started.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Implementation_task_cannot_be_completed_while_scheduled()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (_, implementation) = await ChangeWorld.ScheduledAsync(world);

        using var response = await world.Desk.PostAsync(
            TaskWorld.PathOf(implementation, "complete"), new { version = TaskWorld.VersionOf(implementation), resultNote = "Bitti" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.phase_not_active", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Implementation_task_starts_once_the_change_is_implementing()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, implementation) = await ChangeWorld.ScheduledAsync(world);
        await ChangeWorld.ActAsync(world.Desk, scheduled, "start-implementation");

        var started = await ChangeWorld.TaskActAsync(world.Desk, implementation, "start");

        Assert.Equal("in_progress", started.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Adding_a_change_task_needs_the_owner_team_and_may_target_any_team()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var outsider = await world.Field.PostAsync(TaskWorld.TasksPath, TaskBody(world.FieldTeam.Id, ChangeWorld.NumberOf(change), "implementation"));
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);

        var task = await ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.FieldTeam.Id);
        var link = task.GetProperty("changeLink");
        Assert.True(link.GetProperty("accessible").GetBoolean());
        Assert.Equal(ChangeWorld.NumberOf(change), link.GetProperty("number").GetString());
        Assert.Equal("implementation", link.GetProperty("phase").GetString());
        Assert.Equal("draft", link.GetProperty("status").GetString());

        // A member of the task's team has the working summary of the change, so the link stays readable.
        var asField = await TicketWorld.ReadAsync(world.Field, TaskWorld.PathOf(task));
        Assert.True(asField.GetProperty("changeLink").GetProperty("accessible").GetBoolean());
    }

    [Fact]
    public async Task Change_link_hides_number_and_title_without_access_to_the_change()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var task = await ChangeWorld.CreateTaskAsync(world.Desk, change, "preparation", world.FieldTeam.Id);
        // Desk still sees the task as its creator, but no longer belongs to the owner team.
        await TestData.SaveAsync(factory, db => db.TeamMemberships.RemoveRange(
            db.TeamMemberships.Where(m => m.UserId == world.DeskUser.Id && m.TeamId == world.ServiceDesk.Id)));

        var read = await TicketWorld.ReadAsync(world.Desk, TaskWorld.PathOf(task));

        var link = read.GetProperty("changeLink");
        Assert.False(link.GetProperty("accessible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, link.GetProperty("number").ValueKind);
        Assert.Equal(JsonValueKind.Null, link.GetProperty("title").ValueKind);
        Assert.Equal("preparation", link.GetProperty("phase").GetString());
    }

    [Fact]
    public async Task Change_task_needs_a_phase_and_a_phase_needs_a_change()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var noPhase = await world.Desk.PostAsync(TaskWorld.TasksPath, TaskBody(world.FieldTeam.Id, ChangeWorld.NumberOf(change), null));
        Assert.Equal(HttpStatusCode.BadRequest, noPhase.StatusCode);
        Assert.Contains("validation.required", (await ApiClient.ReadFieldCodesAsync(noPhase))["phase"]);

        using var noChange = await world.Desk.PostAsync(TaskWorld.TasksPath, TaskBody(world.FieldTeam.Id, null, "implementation"));
        Assert.Equal(HttpStatusCode.BadRequest, noChange.StatusCode);
        Assert.True((await ApiClient.ReadFieldCodesAsync(noChange)).ContainsKey("phase"));
    }

    [Fact]
    public async Task Change_task_cannot_also_link_a_ticket()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var response = await world.Desk.PostAsync(
            TaskWorld.TasksPath, TaskBody(world.FieldTeam.Id, ChangeWorld.NumberOf(change), "implementation", TicketWorld.NumberOf(ticket)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tasks.one_link_only", (await ApiClient.ReadFieldCodesAsync(response))["changeNumber"]);
    }

    [Fact]
    public async Task Adding_an_implementation_task_while_scheduled_voids_the_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);

        await ChangeWorld.CreateTaskAsync(world.Desk, scheduled, "implementation", world.FieldTeam.Id);

        var detail = await ChangeWorld.DetailAsync(world.Desk, scheduled);
        Assert.Equal("pending_approval", detail.GetProperty("status").GetString());
        var approvals = await ApprovalsAsync(scheduled);
        Assert.Equal([ChangeApprovalStatus.Void, ChangeApprovalStatus.Pending], approvals.Select(a => a.Status));
        Assert.NotEqual(approvals[0].Fingerprint, approvals[1].Fingerprint);
    }

    [Fact]
    public async Task Adding_a_preparation_task_while_scheduled_keeps_the_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);

        await ChangeWorld.CreateTaskAsync(world.Desk, scheduled, "preparation", world.FieldTeam.Id);

        Assert.Equal("scheduled", (await ChangeWorld.DetailAsync(world.Desk, scheduled)).GetProperty("status").GetString());
        Assert.Equal(ChangeApprovalStatus.Approved, Assert.Single(await ApprovalsAsync(scheduled)).Status);
    }

    [Fact]
    public async Task Cancelling_an_implementation_task_while_scheduled_voids_the_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, task) = await ChangeWorld.ScheduledAsync(world,
            change => ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.FieldTeam.Id));

        await ChangeWorld.TaskActAsync(world.Desk, task, "cancel", new { version = TaskWorld.VersionOf(task), reason = "Gereksiz" });

        Assert.Equal("pending_approval", (await ChangeWorld.DetailAsync(world.Desk, scheduled)).GetProperty("status").GetString());
        Assert.Equal([ChangeApprovalStatus.Void, ChangeApprovalStatus.Pending], (await ApprovalsAsync(scheduled)).Select(a => a.Status));
    }

    [Fact]
    public async Task Cancelling_the_last_implementation_task_of_a_scheduled_change_is_not_ready()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, task) = await ChangeWorld.ScheduledAsync(world);

        using var response = await world.Desk.PostAsync(TaskWorld.PathOf(task, "cancel"), new { version = TaskWorld.VersionOf(task), reason = "Gereksiz" });

        Assert.Equal("changes.not_ready", await ApiClient.ReadCodeAsync(response));
        Assert.Equal("open", (await TicketWorld.ReadAsync(world.Desk, TaskWorld.PathOf(task))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reopening_a_cancelled_implementation_task_while_scheduled_voids_the_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);
        JsonElement second = default;
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world, async change =>
        {
            second = await ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.ServiceDesk.Id);
            second = await ChangeWorld.TaskActAsync(world.Desk, second, "cancel", new { version = TaskWorld.VersionOf(second), reason = "Sonra" });
        });

        await ChangeWorld.TaskActAsync(world.Desk, second, "reopen");

        Assert.Equal([ChangeApprovalStatus.Void, ChangeApprovalStatus.Pending], (await ApprovalsAsync(scheduled)).Select(a => a.Status));
    }

    [Fact]
    public async Task Adding_an_implementation_task_while_pending_refreshes_the_fingerprint_and_the_actor_becomes_the_plan_editor()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.FieldTeam.Id);
        var submitted = await ChangeWorld.ActAsync(world.Desk, await ChangeWorld.DetailAsync(world.Desk, change), "submit");
        var before = Assert.Single(await ApprovalsAsync(submitted));

        await ChangeWorld.CreateTaskAsync(world.Admin, submitted, "implementation", world.FieldTeam.Id);

        var after = Assert.Single(await ApprovalsAsync(submitted));
        Assert.Equal(ChangeApprovalStatus.Pending, after.Status);
        Assert.NotEqual(before.Fingerprint, after.Fingerprint);
        Assert.Equal(world.AdminUser.Id, after.PlanEditorId);
    }

    [Fact]
    public async Task Implementation_tasks_are_locked_from_implementing_on_but_backout_tasks_can_be_added()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, task) = await ChangeWorld.ScheduledAsync(world);
        await ChangeWorld.ActAsync(world.Desk, scheduled, "start-implementation");

        using var add = await world.Desk.PostAsync(TaskWorld.TasksPath, TaskBody(world.FieldTeam.Id, ChangeWorld.NumberOf(scheduled), "implementation"));
        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        Assert.Equal("changes.plan_locked", await ApiClient.ReadCodeAsync(add));

        using var cancel = await world.Desk.PostAsync(TaskWorld.PathOf(task, "cancel"), new { version = TaskWorld.VersionOf(task), reason = "Gereksiz" });
        Assert.Equal("changes.plan_locked", await ApiClient.ReadCodeAsync(cancel));

        var backout = await ChangeWorld.CreateTaskAsync(world.Desk, scheduled, "backout", world.FieldTeam.Id);
        Assert.Equal("backout", backout.GetProperty("changeLink").GetProperty("phase").GetString());
    }

    [Fact]
    public async Task Backout_task_starts_only_after_the_backout_started()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);
        var implementing = await ChangeWorld.ActAsync(world.Desk, scheduled, "start-implementation");
        var backout = await ChangeWorld.CreateTaskAsync(world.Desk, implementing, "backout", world.ServiceDesk.Id, world.DeskUser.Id);

        Assert.Equal("changes.phase_not_active", await StartCodeAsync(world.Desk, backout));

        await ChangeWorld.ActAsync(world.Desk, implementing, "start-backout");
        Assert.Null(await StartCodeAsync(world.Desk, backout));
    }

    [Fact]
    public async Task Resume_follows_the_phase_gate()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, task) = await ChangeWorld.ScheduledAsync(world);
        var held = await ChangeWorld.TaskActAsync(world.Desk, task, "hold", new { version = TaskWorld.VersionOf(task), reason = "vendor" });

        using var resume = await world.Desk.PostAsync(TaskWorld.PathOf(held, "resume"), new { version = TaskWorld.VersionOf(held) });

        Assert.Equal("changes.phase_not_active", await ApiClient.ReadCodeAsync(resume));
        Assert.Equal("scheduled", (await ChangeWorld.DetailAsync(world.Desk, scheduled)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Adding_a_task_to_a_closed_change_is_an_invalid_transition()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.ActAsync(world.Desk, change, "cancel", new { version = ChangeWorld.VersionOf(change), reason = "Vazgeçildi" });

        using var response = await world.Desk.PostAsync(TaskWorld.TasksPath, TaskBody(world.FieldTeam.Id, ChangeWorld.NumberOf(change), "preparation"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.invalid_transition", await ApiClient.ReadCodeAsync(response));
    }
}
