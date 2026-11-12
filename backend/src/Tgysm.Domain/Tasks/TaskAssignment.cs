using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tasks;

/// <summary>Append-only history of a task's team and assignee changes.</summary>
public sealed class TaskAssignment
{
    public const int NoteMaxLength = 500;

    private TaskAssignment()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid TaskId { get; private init; }
    public Guid FromTeamId { get; private init; }
    public Guid ToTeamId { get; private init; }
    public Guid? FromAssigneeId { get; private init; }
    public Guid? ToAssigneeId { get; private init; }
    public TransferReason? Reason { get; private init; }
    public string? Note { get; private init; }
    public Guid ActorUserId { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>History row of a claim, which is written by a conditional update outside the aggregate.</summary>
    public static TaskAssignment Claim(Guid taskId, Guid teamId, Guid assigneeId, DateTimeOffset now) =>
        Record(taskId, teamId, teamId, null, assigneeId, null, null, assigneeId, now);

    internal static TaskAssignment Record(
        Guid taskId, Guid fromTeamId, Guid toTeamId, Guid? fromAssigneeId, Guid? toAssigneeId, TransferReason? reason, string? note, Guid actorUserId, DateTimeOffset now) =>
        new()
        {
            TaskId = taskId,
            FromTeamId = fromTeamId,
            ToTeamId = toTeamId,
            FromAssigneeId = fromAssigneeId,
            ToAssigneeId = toAssigneeId,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ActorUserId = actorUserId,
            OccurredAt = now,
        };
}
