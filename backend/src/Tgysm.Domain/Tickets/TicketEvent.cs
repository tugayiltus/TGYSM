namespace Tgysm.Domain.Tickets;

/// <summary>Append-only timeline row. <see cref="DataJson"/> holds kind-specific details (no secrets).</summary>
public sealed class TicketEvent
{
    private TicketEvent()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid TicketId { get; private init; }
    public TicketEventKind Kind { get; private init; }
    public Visibility Visibility { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string DataJson { get; private init; } = "{}";
    public DateTimeOffset OccurredAt { get; private init; }

    public static TicketEvent Record(Guid ticketId, TicketEventKind kind, Visibility visibility, Guid? actorUserId, string dataJson, DateTimeOffset now) =>
        new() { TicketId = ticketId, Kind = kind, Visibility = visibility, ActorUserId = actorUserId, DataJson = dataJson, OccurredAt = now };
}
