using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tickets;

/// <summary>Append-only timeline row. <see cref="DataJson"/> holds kind-specific details (no secrets).</summary>
public sealed class TicketEvent
{
    private TicketEvent()
    {
    }

    // Sequential: the timeline orders by time, then id, and several events share one instant.
    public Guid Id { get; private init; } = SequentialId.Next();
    public Guid TicketId { get; private init; }
    public TicketEventKind Kind { get; private init; }
    public Visibility Visibility { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string DataJson { get; private init; } = "{}";
    public DateTimeOffset OccurredAt { get; private init; }

    public static TicketEvent Record(Guid ticketId, TicketEventKind kind, Visibility visibility, Guid? actorUserId, string dataJson, DateTimeOffset now) =>
        new() { TicketId = ticketId, Kind = kind, Visibility = visibility, ActorUserId = actorUserId, DataJson = dataJson, OccurredAt = now };
}
