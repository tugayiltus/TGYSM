namespace Tgysm.Domain.Notifications;

/// <summary>Lifecycle of an <see cref="EmailDelivery"/> (spec §3.1, ADR-0015).</summary>
public enum DeliveryStatus
{
    /// <summary>Inserted by the outbox dispatcher; not yet claimed by <c>EmailSender</c>.</summary>
    Pending,

    /// <summary>Claimed by <c>EmailSender</c> (leased) and in the process of being sent.</summary>
    Sending,

    /// <summary>Handed to SMTP (or the pickup folder) and confirmed sent.</summary>
    Sent,

    /// <summary>Every attempt (up to the configured maximum) failed; visible and retryable from `/admin/email-deliveries`.</summary>
    Failed,
}
