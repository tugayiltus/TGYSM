namespace Tgysm.Domain.Notifications;

/// <summary>
/// Transactional outbox row (ADR-0015): a handler adds one in the same save as its change, through
/// <c>INotificationOutbox</c>; the worker's <c>OutboxDispatcher</c> later resolves recipients, renders email and
/// moves it to <see cref="OutboxStatus.Dispatched"/> or <see cref="OutboxStatus.Skipped"/>.
/// </summary>
public sealed class OutboxEvent
{
    public const int EventTypeMaxLength = 50;

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

    public OutboxStatus Status { get; private init; } = OutboxStatus.Pending;

    public DateTimeOffset CreatedAt { get; private init; }

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
}
