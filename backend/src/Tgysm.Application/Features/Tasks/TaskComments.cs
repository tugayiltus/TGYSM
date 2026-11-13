using System.Text.Json;
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

public sealed record GetTaskTimelineQuery(string Number);

public sealed record TaskTimelineItem(Guid Id, TaskEventKind Kind, string? ActorName, JsonElement Data, DateTimeOffset OccurredAt);

/// <summary>Task history and comments in order; every event is internal, so the task scope is the only gate.</summary>
public sealed class GetTaskTimelineHandler(IAppDbContext db, TaskScope scope)
{
    private const string NameSeparator = " ";

    public async Task<IReadOnlyList<TaskTimelineItem>> HandleAsync(GetTaskTimelineQuery query, CancellationToken cancellationToken)
    {
        var (task, _) = await scope.LoadAsync(query.Number, cancellationToken);
        var rows = await db.TaskEvents.AsNoTracking().Where(e => e.TaskId == task.Id)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.Kind,
                e.DataJson,
                e.OccurredAt,
                Actor = db.Users.Where(u => u.Id == e.ActorUserId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new TaskTimelineItem(r.Id, r.Kind, r.Actor, JsonSerializer.Deserialize<JsonElement>(r.DataJson), r.OccurredAt))];
    }
}
