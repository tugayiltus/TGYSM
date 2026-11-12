using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Tasks;

namespace Tgysm.Application.Features.Tasks;

/// <summary>
/// Common frame of a task action, as <see cref="TicketWork"/> for tickets: load in scope (404 outside), check the
/// expected version, run the change, save once (task, history, audit, outbox together) and return the fresh detail.
/// </summary>
public sealed class TaskWork(IAppDbContext db, TaskScope scope, TicketScope tickets, ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<TaskDetail> RunAsync(
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

    public Task<TaskDetail> DetailAsync(Guid taskId, TaskViewer viewer, CancellationToken cancellationToken) =>
        TaskQueries.LoadDetailAsync(db, tickets, companyClock, clock, taskId, viewer, cancellationToken);
}
