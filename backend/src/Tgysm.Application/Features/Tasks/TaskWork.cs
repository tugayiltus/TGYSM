using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Changes;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Tasks;

namespace Tgysm.Application.Features.Tasks;

/// <summary>
/// Common frame of a task action, as <see cref="TicketWork"/> for tickets: load in scope (404 outside), check the
/// expected version, run the change, save once (task, history, audit, outbox together) and return the fresh detail.
/// A task of a change runs inside the change lock, so it serializes with the change's state changes (ADR-0032).
/// </summary>
public sealed class TaskWork(
    IAppDbContext db, TaskScope scope, TicketScope tickets, ChangeScope changes, ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<TaskDetail> RunAsync(
        string number, uint version, bool requireWorker, Func<WorkTask, TaskViewer, DateTimeOffset, Task> change, CancellationToken cancellationToken)
    {
        if (!await IsChangeTaskAsync(number, cancellationToken))
        {
            return await RunCoreAsync(number, version, requireWorker, change, cancellationToken);
        }

        TaskDetail? detail = null;
        await db.RunSerializedAsync(
            SerializationLocks.ChangeWork, async ct => detail = await RunCoreAsync(number, version, requireWorker, change, ct), cancellationToken);
        return detail!;
    }

    /// <summary>Runs <paramref name="work"/> in a transaction; for a task of a change, inside the change lock.</summary>
    public Task InFrameAsync(bool changeTask, Func<CancellationToken, Task> work, CancellationToken cancellationToken) =>
        changeTask
            ? db.RunSerializedAsync(SerializationLocks.ChangeWork, work, cancellationToken)
            : db.RunInTransactionAsync(work, cancellationToken);

    public Task<TaskDetail> DetailAsync(Guid taskId, TaskViewer viewer, CancellationToken cancellationToken) =>
        TaskQueries.LoadDetailAsync(db, tickets, changes, companyClock, clock, taskId, viewer, cancellationToken);

    // The change link is fixed after creation, so reading it before the lock is safe.
    private Task<bool> IsChangeTaskAsync(string number, CancellationToken cancellationToken)
    {
        var normalized = TaskNumber.Normalize(number);
        return db.WorkTasks.AnyAsync(t => t.Number == normalized && t.ChangeId != null, cancellationToken);
    }

    private async Task<TaskDetail> RunCoreAsync(
        string number, uint version, bool requireWorker, Func<WorkTask, TaskViewer, DateTimeOffset, Task> change, CancellationToken cancellationToken)
    {
        var (task, viewer) = requireWorker
            ? await scope.LoadForWorkAsync(number, cancellationToken)
            : await scope.LoadAsync(number, cancellationToken);
        db.SetExpectedVersion(task, version);
        await change(task, viewer, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(task.Id, viewer, cancellationToken);
    }
}
