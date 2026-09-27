namespace Tgysm.Domain.Notifications;

/// <summary>Lifecycle of a <see cref="OutboxEvent"/> (ADR-0015).</summary>
public enum OutboxStatus
{
    /// <summary>Written by a handler; not yet picked up by the worker's <c>OutboxDispatcher</c>.</summary>
    Pending,

    /// <summary>The dispatcher resolved at least one recipient and inserted deliveries for it.</summary>
    Dispatched,

    /// <summary>The dispatcher found no recipient (e.g. an inactive user); nothing to send.</summary>
    Skipped,
}
