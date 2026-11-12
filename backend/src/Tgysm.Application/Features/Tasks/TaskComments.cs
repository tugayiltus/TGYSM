using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Tasks;

namespace Tgysm.Application.Features.Tasks;

public sealed record AddTaskCommentCommand(string Number, string Body);

internal sealed class AddTaskCommentValidator : AbstractValidator<AddTaskCommentCommand>
{
    public AddTaskCommentValidator() => RuleFor(c => c.Body).NotEmpty().MaximumLength(WorkTask.NoteMaxLength);
}

/// <summary>
/// An internal note in the task history. Like ticket comments it needs no version and touches only
/// <see cref="WorkTask.UpdatedAt"/>, so a note never conflicts with a status change.
/// </summary>
public sealed class AddTaskCommentHandler(IAppDbContext db, IValidator<AddTaskCommentCommand> validator, TaskScope scope, TimeProvider clock)
{
    public async Task HandleAsync(AddTaskCommentCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var (task, viewer) = await scope.LoadForWorkAsync(command.Number, cancellationToken);
        var now = clock.GetUtcNow();
        TaskHistory.Record(db, task, TaskEventKind.Commented, viewer.UserId, now, new { body = command.Body.Trim() });
        await db.WorkTasks.Where(t => t.Id == task.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedAt, now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
