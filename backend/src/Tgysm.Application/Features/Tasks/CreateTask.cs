using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Changes;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tasks;

/// <param name="TicketNumber">Optional incident or request the task belongs to; fixed after creation.</param>
/// <param name="ChangeNumber">Optional change the task belongs to (not with a ticket); fixed after creation.</param>
/// <param name="Phase">Phase of a change task; required exactly when <paramref name="ChangeNumber"/> is set.</param>
public sealed record CreateTaskCommand(
    string Title, string? Description, Guid TeamId, Guid? AssigneeId, Priority Priority, DateOnly? DueDate, string? TicketNumber,
    string? ChangeNumber = null, ChangePhase? Phase = null);

internal sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(WorkTask.TitleMaxLength);
        RuleFor(c => c.Description).MaximumLength(WorkTask.DescriptionMaxLength);
        RuleFor(c => c.TeamId).NotEmpty();
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.ChangeNumber).Empty().When(c => !string.IsNullOrWhiteSpace(c.TicketNumber)).WithErrorCode(ErrorCodes.TaskOneLinkOnly);
        RuleFor(c => c.Phase).NotNull().IsInEnum().When(c => !string.IsNullOrWhiteSpace(c.ChangeNumber));
        RuleFor(c => c.Phase).Null().When(c => string.IsNullOrWhiteSpace(c.ChangeNumber));
    }
}

/// <summary>
/// IT staff open a task for any team. A task linked to a ticket needs work access to that ticket; the ticket itself
/// does not change (ADR-0031). A task of a change needs work access to the change (owner team or admin) and may go to
/// any team; it is created inside the change lock, and an Uygulama task changes the plan the approval covers (ADR-0032).
/// </summary>
public sealed class CreateTaskHandler(
    IAppDbContext db, IValidator<CreateTaskCommand> validator, ITicketNumberGenerator numbers, TaskScope scope, TicketScope tickets,
    ChangeScope changes, ChangeApprovalFlow flow, TaskWork work, IAuditLog audit, INotificationOutbox outbox, TimeProvider clock)
{
    public async Task<TaskDetail> HandleAsync(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        var viewer = await scope.StaffViewerAsync(cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        Guid? ticketId = null;
        if (!string.IsNullOrWhiteSpace(command.TicketNumber))
        {
            var (ticket, _) = await tickets.LoadForWorkAsync(command.TicketNumber, cancellationToken);
            ticketId = ticket.Id;
        }

        await TaskRules.EnsureActiveTeamAsync(db, command.TeamId, cancellationToken);
        if (command.AssigneeId is { } assignee)
        {
            await TaskRules.EnsureAssigneeInTeamAsync(db, command.TeamId, assignee, cancellationToken);
        }

        var changeNumber = string.IsNullOrWhiteSpace(command.ChangeNumber) ? null : command.ChangeNumber;
        WorkTask? task = null;
        await work.InFrameAsync(changeNumber is not null, async ct =>
        {
            ChangeRequest? change = null;
            if (changeNumber is not null)
            {
                (change, _) = await changes.LoadForWorkAsync(changeNumber, ct);
                ChangeTaskRules.RequireCanJoin(change, command.Phase!.Value);
            }

            var now = clock.GetUtcNow();
            var number = TaskNumber.Format(now.Year, await numbers.NextAsync(TaskNumber.CounterKind, now.Year, ct));
            task = WorkTask.Create(
                number, command.Title, command.Description, command.TeamId, command.AssigneeId, command.Priority, command.DueDate,
                ticketId, change?.Id, change is null ? null : command.Phase, viewer.UserId, now);
            db.WorkTasks.Add(task);
            TaskHistory.Record(db, task, TaskEventKind.Created, viewer.UserId, now, new { task.TeamId, task.AssigneeId, task.TicketId, task.ChangeId, task.Phase });
            audit.Record(AuditActions.TaskCreated, AuditEntityTypes.Task, task.Id, newValues: new { task.Number, task.TeamId, task.AssigneeId, task.TicketId, task.ChangeId, task.Phase });
            if (task.AssigneeId is { } assigned)
            {
                outbox.AddForSubject(NotificationEventTypes.TaskAssigned, OutboxSubjectTypes.Task, task.Id, assigned);
            }

            await db.SaveChangesAsync(ct);
            if (change is not null && task.Phase == ChangePhase.Implementation)
            {
                // The plan tasks are read back from the database, so the new task is saved before the approval follows.
                await flow.RecordEditorAsync(change.Id, viewer.UserId, ct);
                await flow.PlanChangedAsync(change, null, viewer.UserId, now, ct);
                await db.SaveChangesAsync(ct);
            }
        }, cancellationToken);
        return await work.DetailAsync(task!.Id, viewer, cancellationToken);
    }
}
