namespace Tgysm.Domain.Notifications;

/// <summary>
/// Transactional outbox row (ADR-0015): a handler adds one in the same save as its change, through
/// <c>INotificationOutbox</c>; the worker's <c>OutboxDispatcher</c> later resolves recipients, renders email and
/// moves it to <see cref="OutboxStatus.Dispatched"/> or <see cref="OutboxStatus.Skipped"/>.
/// </summary>
public sealed class OutboxEvent
{
    public const int EventTypeMaxLength = 50;

    /// <summary>
    /// <see cref="Payload"/> key holding the acting user's id (the person who caused the event), so a later
    /// dispatcher can exclude them from a recipient list where spec §3.3 says "not sent to: the actor/author".
    /// Written by <c>NotificationOutbox</c> on every event; read back with <c>JsonDocument</c>, not deserialized
    /// into a typed model, since the rest of the payload is event-specific.
    /// </summary>
    public const string ActorIdKey = "actorId";

    private OutboxEvent()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>One of <see cref="NotificationEventTypes"/>.</summary>
    public string EventType { get; private init; } = string.Empty;

    public Guid? TicketId { get; private init; }

    /// <summary>The user this event is about (e.g. the new assignee, the reply's author); recipients are resolved separately.</summary>
    public Guid? UserId { get; private init; }

    /// <summary>Ids and reason codes only (jsonb); never subject or resolution text.</summary>
    public string Payload { get; private init; } = "{}";

    /// <summary>Data Protection–encrypted account-link token; account events only.</summary>
    public string? ProtectedSecret { get; private init; }

    public OutboxStatus Status { get; private set; } = OutboxStatus.Pending;

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>Set by <c>RowLease</c> while the dispatcher holds this row; cleared implicitly once it moves out of <see cref="OutboxStatus.Pending"/>.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>When the dispatcher moved this event to <see cref="OutboxStatus.Dispatched"/> or <see cref="OutboxStatus.Skipped"/>.</summary>
    public DateTimeOffset? DispatchedAt { get; private set; }

    public static OutboxEvent Create(string eventType, Guid? ticketId, Guid? userId, string payload, string? protectedSecret, DateTimeOffset now) =>
        new()
        {
            EventType = eventType,
            TicketId = ticketId,
            UserId = userId,
            Payload = payload,
            ProtectedSecret = protectedSecret,
            CreatedAt = now,
        };

    /// <summary>The dispatcher resolved at least one recipient and inserted deliveries for it (spec §3.2 step 2).</summary>
    public void MarkDispatched(DateTimeOffset now)
    {
        Status = OutboxStatus.Dispatched;
        DispatchedAt = now;
    }

    /// <summary>The dispatcher found no recipient (none resolved, or every candidate was inactive); nothing to send.</summary>
    public void MarkSkipped(DateTimeOffset now)
    {
        Status = OutboxStatus.Skipped;
        DispatchedAt = now;
    }
}
