using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Tasks;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tasks;

namespace Tgysm.Application.Features.Changes;

public sealed record StartImplementationCommand(string Number, uint Version);

public sealed record FinishImplementationCommand(string Number, uint Version);

public sealed record StartBackoutCommand(string Number, uint Version);

/// <param name="ActualEnd">When the work ended; the closing time when null.</param>
public sealed record CloseChangeCommand(
    string Number, uint Version, ChangeOutcome Outcome, string? VerificationNote, string? BackoutNote, string? ClosingNote, DateTimeOffset? ActualEnd);

public sealed record CancelChangeCommand(string Number, uint Version, string Reason);

internal sealed class CloseChangeValidator : AbstractValidator<CloseChangeCommand>
{
    public CloseChangeValidator()
    {
        RuleFor(c => c.Number).NotEmpty();
        RuleFor(c => c.Outcome).IsInEnum();
        RuleFor(c => c.VerificationNote).MaximumLength(ChangeRequest.TextMaxLength);
        RuleFor(c => c.BackoutNote).MaximumLength(ChangeRequest.TextMaxLength);
        RuleFor(c => c.ClosingNote).MaximumLength(ChangeRequest.TextMaxLength);
    }
}

internal sealed class CancelChangeValidator : AbstractValidator<CancelChangeCommand>
{
    public CancelChangeValidator()
    {
        RuleFor(c => c.Number).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(ChangeRequest.ReasonMaxLength);
    }
}

/// <summary>Status change of a change: history row and audit entry in the same save.</summary>
internal static class ChangeStatusChange
{
    public static void Record(
        IAppDbContext db, IAuditLog audit, ChangeRequest change, ChangeStatus before, ChangeEventKind kind, Guid actorId, DateTimeOffset now,
        object? data = null, string? reason = null)
    {
        ChangeHistory.Record(db, change, kind, actorId, now, data);
        audit.Record(AuditActions.ChangeStatusChanged, AuditEntityTypes.Change, change.Id, new { Status = before }, new { change.Number, change.Status }, reason);
    }
}

/// <summary>The tasks of a change, loaded for closing or cancelling it, and what those do to them.</summary>
internal static class ChangeTasks
{
    /// <summary>Reason given to the open Geri dönüş tasks of a successful change ("not needed").</summary>
    public const string BackoutNotNeededReason = "Gerekmedi";

    public static Task<List<WorkTask>> LoadAsync(IAppDbContext db, Guid changeId, CancellationToken cancellationToken) =>
        db.WorkTasks.Where(t => t.ChangeId == changeId).ToListAsync(cancellationToken);

    public static ChangeTaskTally Tally(IReadOnlyList<WorkTask> tasks) =>
        new(Of(tasks, ChangePhase.Preparation), Of(tasks, ChangePhase.Implementation), Of(tasks, ChangePhase.Verification), Of(tasks, ChangePhase.Backout));

    /// <summary>Cancels every open task in <paramref name="tasks"/> with the reason; each gets a history row and an audit entry.</summary>
    public static void CancelOpen(IAppDbContext db, IAuditLog audit, IEnumerable<WorkTask> tasks, string reason, Guid actorId, DateTimeOffset now)
    {
        foreach (var task in tasks.Where(t => t.IsOpen))
        {
            var before = task.Status;
            task.Cancel(reason, now);
            TaskStatusChange.Record(db, audit, task, before, TaskEventKind.Cancelled, actorId, now, new { Reason = reason });
        }
    }

    private static ChangePhaseTally Of(IReadOnlyList<WorkTask> tasks, ChangePhase phase)
    {
        var ofPhase = tasks.Where(t => t.Phase == phase).ToList();
        return new ChangePhaseTally(
            ofPhase.Count(t => t.IsOpen), ofPhase.Count(t => t.Status == WorkTaskStatus.Completed), ofPhase.Count(t => t.Status == WorkTaskStatus.Cancelled));
    }
}

/// <summary>Scheduled → implementing; Uygulama tasks may start from now on (ADR-0032).</summary>
public sealed class StartImplementationHandler(IAppDbContext db, ChangeWork work, IAuditLog audit)
{
    public Task<ChangeDetail> HandleAsync(StartImplementationCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, (change, viewer, now, _) =>
        {
            var before = change.Status;
            change.StartImplementation(now);
            ChangeStatusChange.Record(db, audit, change, before, ChangeEventKind.ImplementationStarted, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
}

/// <summary>Implementing → verifying; Doğrulama tasks may start from now on.</summary>
public sealed class FinishImplementationHandler(IAppDbContext db, ChangeWork work, IAuditLog audit)
{
    public Task<ChangeDetail> HandleAsync(FinishImplementationCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, (change, viewer, now, _) =>
        {
            var before = change.Status;
            change.FinishImplementation(now);
            ChangeStatusChange.Record(db, audit, change, before, ChangeEventKind.ImplementationFinished, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
}

/// <summary>Starts the rollback (state stays); Geri dönüş tasks may start and their workers are told.</summary>
public sealed class StartBackoutHandler(IAppDbContext db, ChangeWork work, IAuditLog audit, INotificationOutbox outbox)
{
    public Task<ChangeDetail> HandleAsync(StartBackoutCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, (change, viewer, now, _) =>
        {
            var before = change.Status;
            change.StartBackout(now);
            ChangeStatusChange.Record(db, audit, change, before, ChangeEventKind.BackoutStarted, viewer.UserId, now);
            outbox.AddForSubject(NotificationEventTypes.ChangeBackoutStarted, OutboxSubjectTypes.Change, change.Id, userId: null);
            return Task.CompletedTask;
        }, cancellationToken);
}

/// <summary>
/// Closes the change with an outcome; the domain checks the task tally and notes. A successful change cancels its open
/// Geri dönüş tasks as not needed. Runs in the change lock, so no task of the change moves while the tally is read.
/// </summary>
public sealed class CloseChangeHandler(IAppDbContext db, IValidator<CloseChangeCommand> validator, ChangeWork work, IAuditLog audit)
{
    public async Task<ChangeDetail> HandleAsync(CloseChangeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, async (change, viewer, now, ct) =>
        {
            var tasks = await ChangeTasks.LoadAsync(db, change.Id, ct);
            var before = change.Status;
            change.Close(command.Outcome, new ChangeClosing(command.VerificationNote, command.BackoutNote, command.ClosingNote, command.ActualEnd), ChangeTasks.Tally(tasks), now);
            if (command.Outcome == ChangeOutcome.Successful)
            {
                ChangeTasks.CancelOpen(db, audit, tasks.Where(t => t.Phase == ChangePhase.Backout), ChangeTasks.BackoutNotNeededReason, viewer.UserId, now);
            }

            ChangeHistory.Record(db, change, ChangeEventKind.Closed, viewer.UserId, now, new { change.Outcome });
            audit.Record(AuditActions.ChangeClosed, AuditEntityTypes.Change, change.Id, new { Status = before }, new { change.Number, change.Status, change.Outcome });
        }, cancellationToken);
    }
}

/// <summary>
/// Cancels a change before implementation; its open tasks are cancelled with the same reason and a pending approval
/// leaves the queue.
/// </summary>
public sealed class CancelChangeHandler(
    IAppDbContext db, IValidator<CancelChangeCommand> validator, ChangeWork work, ChangeApprovalFlow flow, IAuditLog audit)
{
    public async Task<ChangeDetail> HandleAsync(CancelChangeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, async (change, viewer, now, ct) =>
        {
            var before = change.Status;
            change.Cancel(command.Reason, now);
            await flow.CloseOpenRequestAsync(change, now, ct);
            ChangeTasks.CancelOpen(db, audit, await ChangeTasks.LoadAsync(db, change.Id, ct), change.CancelReason!, viewer.UserId, now);
            ChangeStatusChange.Record(db, audit, change, before, ChangeEventKind.Cancelled, viewer.UserId, now, new { Reason = change.CancelReason }, change.CancelReason);
        }, cancellationToken);
    }
}
