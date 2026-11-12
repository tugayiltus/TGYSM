using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tasks;

/// <param name="TicketNumber">Optional incident or request the task belongs to; fixed after creation.</param>
public sealed record CreateTaskCommand(
    string Title, string? Description, Guid TeamId, Guid? AssigneeId, Priority Priority, DateOnly? DueDate, string? TicketNumber);

internal sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(WorkTask.TitleMaxLength);
        RuleFor(c => c.Description).MaximumLength(WorkTask.DescriptionMaxLength);
        RuleFor(c => c.TeamId).NotEmpty();
        RuleFor(c => c.Priority).IsInEnum();
    }
}

/// <summary>
/// IT staff open a task for any team. A task linked to a ticket needs work access to that ticket; the ticket itself
/// does not change (ADR-0031).
/// </summary>
public sealed class CreateTaskHandler(
    IAppDbContext db, IValidator<CreateTaskCommand> validator, ITicketNumberGenerator numbers, TaskScope scope, TicketScope tickets,
    TaskWork work, IAuditLog audit, INotificationOutbox outbox, TimeProvider clock)
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

        WorkTask? task = null;
        await db.RunInTransactionAsync(async ct =>
        {
            var now = clock.GetUtcNow();
            var number = TaskNumber.Format(now.Year, await numbers.NextAsync(TaskNumber.CounterKind, now.Year, ct));
            task = WorkTask.Create(number, command.Title, command.Description, command.TeamId, command.AssigneeId, command.Priority, command.DueDate, ticketId, viewer.UserId, now);
            db.WorkTasks.Add(task);
            TaskHistory.Record(db, task, TaskEventKind.Created, viewer.UserId, now, new { task.TeamId, task.AssigneeId, task.TicketId });
            audit.Record(AuditActions.TaskCreated, AuditEntityTypes.Task, task.Id, newValues: new { task.Number, task.TeamId, task.AssigneeId, task.TicketId });
            if (task.AssigneeId is { } assigned)
            {
                outbox.AddForSubject(NotificationEventTypes.TaskAssigned, OutboxSubjectTypes.Task, task.Id, assigned);
            }

            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await work.DetailAsync(task!.Id, viewer, cancellationToken);
    }
}
