using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tasks;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Tasks;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeClosingTests(ApiFactory factory) : IAsyncLifetime
{
    private const string VerificationNote = "Paylaşımlar açılıyor";
    private const string BackoutNote = "Eski imaj yüklendi";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object CloseBody(JsonElement change, string outcome, string? verificationNote = VerificationNote, string? backoutNote = null) =>
        new { version = ChangeWorld.VersionOf(change), outcome, verificationNote, backoutNote, closingNote = "Tamam", actualEnd = (string?)null };

    private static async Task<HttpResponseMessage> CloseAsync(ApiClient client, JsonElement change, object body) =>
        await client.PostAsync(ChangeWorld.PathOf(change, "close"), body);

    private static Task<JsonElement> CompleteAsync(ApiClient client, JsonElement task) =>
        ChangeWorld.TaskActAsync(client, task, "complete", new { version = TaskWorld.VersionOf(task), resultNote = "Bitti" });

    private static Task<JsonElement> ReadTaskAsync(ApiClient client, JsonElement task) => TicketWorld.ReadAsync(client, TaskWorld.PathOf(task));

    /// <summary>
    /// A verifying change: its Uygulama task started (completed unless <paramref name="completeImplementation"/> is false)
    /// and one completed Doğrulama task.
    /// </summary>
    private static async Task<(JsonElement Change, JsonElement Implementation)> VerifyingAsync(TicketWorld world, bool completeImplementation = true)
    {
        var (scheduled, implementation) = await ChangeWorld.ScheduledAsync(world);
        var implementing = await ChangeWorld.ActAsync(world.Desk, scheduled, "start-implementation");
        implementation = await ChangeWorld.TaskActAsync(world.Desk, implementation, "start");
        if (completeImplementation)
        {
            implementation = await CompleteAsync(world.Desk, implementation);
        }

        var verification = await ChangeWorld.CreateTaskAsync(world.Desk, implementing, "verification", world.ServiceDesk.Id, world.DeskUser.Id);
        var verifying = await ChangeWorld.ActAsync(world.Desk, implementing, "finish-implementation");
        verification = await ChangeWorld.TaskActAsync(world.Desk, verification, "start");
        await CompleteAsync(world.Desk, verification);
        return (await ChangeWorld.DetailAsync(world.Desk, verifying), implementation);
    }

    private async Task<WorkTask> TaskRowAsync(JsonElement task)
    {
        var id = task.GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == id, Ct);
    }

    [Fact]
    public async Task Successful_close_is_refused_while_an_implementation_task_is_open()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (verifying, _) = await VerifyingAsync(world, completeImplementation: false);

        using var response = await CloseAsync(world.Desk, verifying, CloseBody(verifying, "successful"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.open_tasks", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Successful_close_needs_a_verification_note()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (verifying, _) = await VerifyingAsync(world);

        using var response = await CloseAsync(world.Desk, verifying, CloseBody(verifying, "successful", verificationNote: null));

        Assert.Equal("changes.verification_required", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Successful_close_cancels_open_backout_tasks_as_not_needed()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (verifying, _) = await VerifyingAsync(world);
        var backout = await ChangeWorld.CreateTaskAsync(world.Desk, verifying, "backout", world.FieldTeam.Id);

        using var response = await CloseAsync(world.Desk, verifying, CloseBody(verifying, "successful"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var closed = await world.Desk.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("closed", closed.GetProperty("status").GetString());
        Assert.Equal("successful", closed.GetProperty("outcome").GetString());
        Assert.Equal(VerificationNote, closed.GetProperty("verificationNote").GetString());
        var row = await TaskRowAsync(backout);
        Assert.Equal(WorkTaskStatus.Cancelled, row.Status);
        Assert.Equal("Gerekmedi", row.CancelReason);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.TaskEvents.AnyAsync(e => e.TaskId == row.Id && e.Kind == TaskEventKind.Cancelled, Ct));
        Assert.True(await db.ChangeEvents.AnyAsync(e => e.ChangeId == row.ChangeId && e.Kind == ChangeEventKind.Closed, Ct));
    }

    [Fact]
    public async Task Rolled_back_needs_a_started_backout_with_closed_backout_tasks_and_a_note()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);
        var implementing = await ChangeWorld.ActAsync(world.Desk, scheduled, "start-implementation");
        var backout = await ChangeWorld.CreateTaskAsync(world.Desk, implementing, "backout", world.ServiceDesk.Id, world.DeskUser.Id);

        using (var notStarted = await CloseAsync(world.Desk, implementing, CloseBody(implementing, "rolled_back", null, BackoutNote)))
        {
            Assert.Equal("changes.backout_required", await ApiClient.ReadCodeAsync(notStarted));
        }

        var backingOut = await ChangeWorld.ActAsync(world.Desk, implementing, "start-backout");
        using (var openTask = await CloseAsync(world.Desk, backingOut, CloseBody(backingOut, "rolled_back", null, BackoutNote)))
        {
            Assert.Equal("changes.backout_required", await ApiClient.ReadCodeAsync(openTask));
        }

        backout = await ChangeWorld.TaskActAsync(world.Desk, backout, "start");
        await CompleteAsync(world.Desk, backout);
        using (var noNote = await CloseAsync(world.Desk, backingOut, CloseBody(backingOut, "rolled_back", null, null)))
        {
            Assert.Equal("changes.backout_required", await ApiClient.ReadCodeAsync(noNote));
        }

        var closed = await ChangeWorld.ActAsync(world.Desk, backingOut, "close", CloseBody(backingOut, "rolled_back", null, BackoutNote));
        Assert.Equal("closed", closed.GetProperty("status").GetString());
        Assert.Equal("rolled_back", closed.GetProperty("outcome").GetString());
        Assert.Equal(BackoutNote, closed.GetProperty("backoutNote").GetString());
    }

    [Fact]
    public async Task Cancel_in_scheduled_cancels_open_tasks_with_the_reason_and_they_cannot_reopen()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, implementation) = await ChangeWorld.ScheduledAsync(world);
        var preparation = await ChangeWorld.CreateTaskAsync(world.Desk, scheduled, "preparation", world.ServiceDesk.Id, world.DeskUser.Id);
        preparation = await ChangeWorld.TaskActAsync(world.Desk, preparation, "start");

        var cancelled = await ChangeWorld.ActAsync(world.Desk, scheduled, "cancel", new { version = ChangeWorld.VersionOf(scheduled), reason = "Vazgeçildi" });

        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal("Vazgeçildi", cancelled.GetProperty("cancelReason").GetString());
        foreach (var task in new[] { implementation, preparation })
        {
            var row = await TaskRowAsync(task);
            Assert.Equal(WorkTaskStatus.Cancelled, row.Status);
            Assert.Equal("Vazgeçildi", row.CancelReason);
        }

        var current = await ReadTaskAsync(world.Desk, preparation);
        using var reopen = await world.Desk.PostAsync(TaskWorld.PathOf(current, "reopen"), new { version = TaskWorld.VersionOf(current) });
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
    }

    [Fact]
    public async Task Cancel_while_pending_takes_the_pending_approval_off_the_queue()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.FieldTeam.Id);
        var submitted = await ChangeWorld.ActAsync(world.Desk, await ChangeWorld.DetailAsync(world.Desk, change), "submit");

        await ChangeWorld.ActAsync(world.Desk, submitted, "cancel", new { version = ChangeWorld.VersionOf(submitted), reason = "Vazgeçildi" });

        var id = submitted.GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ChangeApprovals.AnyAsync(a => a.ChangeId == id && a.Status == ChangeApprovalStatus.Pending, Ct));
    }

    [Fact]
    public async Task Cancel_after_implementation_started_is_an_invalid_transition()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);
        var implementing = await ChangeWorld.ActAsync(world.Desk, scheduled, "start-implementation");

        using var response = await world.Desk.PostAsync(ChangeWorld.PathOf(implementing, "cancel"), new { version = ChangeWorld.VersionOf(implementing), reason = "Vazgeçildi" });

        Assert.Equal("changes.invalid_transition", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Task_complete_and_successful_close_in_parallel_never_close_with_an_open_implementation_task()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (verifying, implementation) = await VerifyingAsync(world, completeImplementation: false);

        var responses = await Task.WhenAll(
            world.Desk.PostAsync(TaskWorld.PathOf(implementation, "complete"), new { version = TaskWorld.VersionOf(implementation), resultNote = "Bitti" }),
            world.Desk.PostAsync(ChangeWorld.PathOf(verifying, "close"), CloseBody(verifying, "successful")));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await AssertNotClosedWithOpenImplementationAsync(verifying);
    }

    [Fact]
    public async Task Task_reopen_and_successful_close_in_parallel_never_close_with_an_open_implementation_task()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (verifying, implementation) = await VerifyingAsync(world);

        var responses = await Task.WhenAll(
            world.Desk.PostAsync(TaskWorld.PathOf(implementation, "reopen"), new { version = TaskWorld.VersionOf(implementation) }),
            world.Desk.PostAsync(ChangeWorld.PathOf(verifying, "close"), CloseBody(verifying, "successful")));
        try
        {
            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await AssertNotClosedWithOpenImplementationAsync(verifying);
    }

    private async Task AssertNotClosedWithOpenImplementationAsync(JsonElement change)
    {
        var id = change.GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var status = await db.ChangeRequests.Where(c => c.Id == id).Select(c => c.Status).SingleAsync(Ct);
        var openImplementation = await db.WorkTasks.AnyAsync(
            t => t.ChangeId == id && t.Phase == ChangePhase.Implementation && t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled, Ct);
        Assert.False(status == ChangeStatus.Closed && openImplementation);
    }
}
