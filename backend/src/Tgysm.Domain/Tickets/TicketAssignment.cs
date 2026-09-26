namespace Tgysm.Domain.Tickets;

/// <summary>Append-only history of team and assignee changes.</summary>
public sealed class TicketAssignment
{
    public const int NoteMaxLength = 500;

    private TicketAssignment()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid TicketId { get; private init; }
    public Guid FromTeamId { get; private init; }
    public Guid ToTeamId { get; private init; }
    public Guid? FromAssigneeId { get; private init; }
    public Guid? ToAssigneeId { get; private init; }
    public TransferReason? Reason { get; private init; }
    public string? Note { get; private init; }
    public Guid ActorUserId { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }

    internal static TicketAssignment Record(
        Guid ticketId, Guid fromTeamId, Guid toTeamId, Guid? fromAssigneeId, Guid? toAssigneeId, TransferReason? reason, string? note, Guid actorUserId, DateTimeOffset now) =>
        new()
        {
            TicketId = ticketId,
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
