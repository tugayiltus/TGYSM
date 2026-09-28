using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Domain.Notifications;

/// <summary>
/// A resolved recipient for one delivery (spec §3.3): the user id is the dedupe key against
/// <see cref="OutboxEvent.ActorIdKey"/> exclusion and the unique (<c>outbox_id</c>, <c>recipient_user_id</c>) index;
/// email and full name feed <c>recipientName</c> and the delivery's envelope. A plain value, not an <see cref="Entity"/>:
/// it is never persisted on its own, only read into an <see cref="EmailDelivery"/>.
/// </summary>
public sealed record Recipient(Guid UserId, string Email, string FullName);

/// <summary>
/// One outgoing email for one recipient of one <see cref="OutboxEvent"/> (spec §3.1, ADR-0015). The dispatcher
/// creates it with <see cref="DeliveryStatus.Pending"/>; <c>EmailSender</c> (a later task) advances it through
/// <see cref="DeliveryStatus.Sending"/> to <see cref="DeliveryStatus.Sent"/> or <see cref="DeliveryStatus.Failed"/>.
/// Account-link events (spec §3.4) are created with <see cref="Body"/> left <c>null</c>: the body is rendered at
/// send time from the outbox event's <c>protected_secret</c>, so the token never sits in a delivery row longer
/// than needed, and is cleared back to a fixed marker once the delivery reaches <c>Sent</c> or <c>Failed</c>.
/// </summary>
public sealed class EmailDelivery : Entity
{
    public const int RecipientEmailMaxLength = User.EmailMaxLength;
    public const int TemplateTypeMaxLength = OutboxEvent.EventTypeMaxLength;
    public const int LastErrorMaxLength = 500;

    private EmailDelivery()
    {
    }

    public Guid OutboxId { get; private init; }

    public Guid RecipientUserId { get; private init; }

    public string RecipientEmail { get; private init; } = string.Empty;

    /// <summary>One of <see cref="NotificationEventTypes"/>; the template this delivery was rendered from.</summary>
    public string TemplateType { get; private init; } = string.Empty;

    public string Subject { get; private init; } = string.Empty;

    /// <summary><c>null</c> for an account-link delivery until it is rendered and sent (see type summary).</summary>
    public string? Body { get; private init; }

    public DeliveryStatus Status { get; private set; } = DeliveryStatus.Pending;

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private init; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public static EmailDelivery Create(Guid outboxId, Recipient recipient, string templateType, string subject, string? body, DateTimeOffset now) =>
        new()
        {
            OutboxId = outboxId,
            RecipientUserId = recipient.UserId,
            RecipientEmail = recipient.Email,
            TemplateType = templateType,
            Subject = subject,
            Body = body,
            NextAttemptAt = now,
        };
}
