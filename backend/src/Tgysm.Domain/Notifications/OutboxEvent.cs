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

    /// <summary>
    /// <see cref="Payload"/> key holding which SLA metric an <c>sla_warning</c>/<c>sla_breached</c> event is about
    /// (e.g. first response, resolution). Written by the SLA monitor (a later task); <c>NotificationContextLoader</c>
    /// reads it back to fill the <c>slaMetric</c> template variable.
    /// </summary>
    public const string SlaMetricKey = "metric";

    /// <summary>
    /// <see cref="Payload"/> key holding that metric's due time (ISO 8601), read back to fill the <c>dueAt</c>
    /// template variable.
    /// </summary>
    public const string SlaDueAtKey = "dueAt";

    /// <summary>Dispatch attempts stop retrying once <see cref="Attempts"/> reaches this (spec: retry plan, max 5 attempts).</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Delay applied after each failed attempt, indexed by <c>Attempts - 1</c> (spec: back-off 1, 5, 15, 60, 360
    /// minutes). An event's <see cref="MaxAttempts"/>-th failure moves it straight to <see cref="OutboxStatus.Failed"/>
    /// instead of scheduling a further retry, so the last entry is never actually applied as a delay; it is kept in
    /// the list because the plan states five values, one per attempt.
    /// </summary>
    private static readonly TimeSpan[] BackoffIntervals =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(360),
    ];

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

    /// <summary>How many dispatch attempts have failed for this event.</summary>
    public int Attempts { get; private set; }

    /// <summary>The most recent dispatch failure's message, truncated (never a secret: dispatch never touches <see cref="ProtectedSecret"/>).</summary>
    public string? LastError { get; private set; }

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

    /// <summary>
    /// Records one failed dispatch attempt: still-pending rows back off per <see cref="BackoffIntervals"/> so the
    /// dispatcher's next batch skips them until <see cref="LockedUntil"/> passes; the <see cref="MaxAttempts"/>-th
    /// failure moves the event to <see cref="OutboxStatus.Failed"/> instead, so one bad event can never blockade a
    /// batch forever (the claim query orders by id, otherwise re-claiming the same never-clearing row every run).
    /// </summary>
    public void RecordFailure(string error, DateTimeOffset now)
    {
        Attempts++;
        LastError = error.Length > EmailDelivery.LastErrorMaxLength ? error[..EmailDelivery.LastErrorMaxLength] : error;

        if (Attempts >= MaxAttempts)
        {
            Status = OutboxStatus.Failed;
            LockedUntil = null;
        }
        else
        {
            LockedUntil = now + BackoffIntervals[Attempts - 1];
        }
    }
}
