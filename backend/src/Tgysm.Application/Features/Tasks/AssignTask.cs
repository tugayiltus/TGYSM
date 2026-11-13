using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tasks;

public sealed record ClaimTaskCommand(string Number, uint Version);

public sealed record AssignTaskCommand(string Number, uint Version, Guid? AssigneeId);

public sealed record TransferTaskCommand(string Number, uint Version, Guid TeamId, Guid? AssigneeId, TransferReason Reason, string? Note);

internal sealed class TransferTaskValidator : AbstractValidator<TransferTaskCommand>
{
    public TransferTaskValidator()
    {
        RuleFor(c => c.TeamId).NotEmpty();
        RuleFor(c => c.Reason).IsInEnum();
        RuleFor(c => c.Note).MaximumLength(TaskAssignment.NoteMaxLength);
        RuleFor(c => c.Note).NotEmpty().When(c => c.Reason == TransferReason.Other);
    }
}

/// <summary>
/// A team member takes an unassigned task. One conditional update on <c>assignee_id IS NULL</c> decides the
/// winner; the loser of a race gets 409 (ADR-0031). The claimer is the actor, so no email is sent.
/// </summary>
public sealed class ClaimTaskHandler(IAppDbContext db, TaskScope scope, TaskWork work, IAuditLog audit, TimeProvider clock)
{
    public async Task<TaskDetail> HandleAsync(ClaimTaskCommand command, CancellationToken cancellationToken)
    {
        var (task, viewer) = await scope.LoadForWorkAsync(command.Number, cancellationToken);
        if (!task.IsOpen)
        {
            throw new DomainException(ErrorCodes.TaskInvalidTransition, "A closed task cannot be claimed.");
        }

        await TaskRules.EnsureAssigneeInTeamAsync(db, task.TeamId, viewer.UserId, cancellationToken);
        var teamId = task.TeamId;
        await db.RunInTransactionAsync(async ct =>
        {
            var now = clock.GetUtcNow();
            // The version and the open state are part of the condition: a task changed or closed since the person
            // saw it is not claimed, even when its assignee is still empty.
            var claimed = await db.WorkTasks
                .Where(t => t.Id == task.Id && t.AssigneeId == null && t.TeamId == teamId && t.Version == command.Version)
                .Where(t => t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.AssigneeId, viewer.UserId).SetProperty(t => t.UpdatedAt, now), ct);
            if (claimed == 0)
            {
                var someoneClaimed = await db.WorkTasks.AnyAsync(t => t.Id == task.Id && t.AssigneeId != null, ct);
                throw someoneClaimed
                    ? new DomainException(ErrorCodes.TaskAlreadyClaimed, "Someone else has claimed the task.")
                    : new DomainException(ErrorCodes.ConcurrencyConflict, "The task changed since it was loaded.");
            }

            db.TaskAssignments.Add(TaskAssignment.Claim(task.Id, teamId, viewer.UserId, now));
            TaskHistory.Record(db, task, TaskEventKind.Assigned, viewer.UserId, now, new { assigneeId = viewer.UserId });
            audit.Record(AuditActions.TaskAssigned, AuditEntityTypes.Task, task.Id, newValues: new { AssigneeId = viewer.UserId });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await work.DetailAsync(task.Id, viewer, cancellationToken);
    }
}

/// <summary>Sets or clears the assignee inside the task's team; the new assignee gets an email unless they acted.</summary>
public sealed class AssignTaskHandler(IAppDbContext db, TaskWork work, IAuditLog audit, INotificationOutbox outbox)
{
    public Task<TaskDetail> HandleAsync(AssignTaskCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, requireWorker: true, async (task, viewer, now) =>
        {
            if (command.AssigneeId is { } assignee)
            {
                await TaskRules.EnsureAssigneeInTeamAsync(db, task.TeamId, assignee, cancellationToken);
            }

            var before = task.AssigneeId;
            db.TaskAssignments.Add(task.Assign(command.AssigneeId, now, viewer.UserId));
            TaskHistory.Record(db, task, TaskEventKind.Assigned, viewer.UserId, now, new { assigneeId = command.AssigneeId });
            audit.Record(AuditActions.TaskAssigned, AuditEntityTypes.Task, task.Id, new { AssigneeId = before }, new { command.AssigneeId });
            if (command.AssigneeId is { } assigned)
            {
                outbox.AddForSubject(NotificationEventTypes.TaskAssigned, OutboxSubjectTypes.Task, task.Id, assigned);
            }
        }, cancellationToken);
}

/// <summary>Moves the task to another active team with a reason; without an assignee the new team is notified.</summary>
public sealed class TransferTaskHandler(IAppDbContext db, IValidator<TransferTaskCommand> validator, TaskWork work, IAuditLog audit, INotificationOutbox outbox)
{
    public async Task<TaskDetail> HandleAsync(TransferTaskCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, async (task, viewer, now) =>
        {
            await TaskRules.EnsureActiveTeamAsync(db, command.TeamId, cancellationToken);
            if (command.AssigneeId is { } assignee)
            {
                await TaskRules.EnsureAssigneeInTeamAsync(db, command.TeamId, assignee, cancellationToken);
            }

            var before = new { task.TeamId, task.AssigneeId };
            db.TaskAssignments.Add(task.Transfer(command.TeamId, command.Reason, command.Note, now, viewer.UserId));
            if (command.AssigneeId is not null)
            {
                db.TaskAssignments.Add(task.Assign(command.AssigneeId, now, viewer.UserId));
            }

            TaskHistory.Record(db, task, TaskEventKind.Transferred, viewer.UserId, now, new { command.TeamId, command.AssigneeId, command.Reason, command.Note });
            audit.Record(AuditActions.TaskTransferred, AuditEntityTypes.Task, task.Id, before, new { command.TeamId, command.AssigneeId, command.Reason });
            outbox.AddForSubject(NotificationEventTypes.TaskAssigned, OutboxSubjectTypes.Task, task.Id, command.AssigneeId);
        }, cancellationToken);
    }
}
