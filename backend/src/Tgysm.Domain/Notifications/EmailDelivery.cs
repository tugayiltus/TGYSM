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

    /// <summary>Column limit of <see cref="Subject"/>; the rendered subject is cut to it (a template plus a long ticket subject can exceed it).</summary>
    public const int SubjectMaxLength = EmailTemplate.SubjectMaxLength;

    /// <summary>Column limit of <see cref="Body"/>; the rendered body is cut to it.</summary>
    public const int BodyMaxLength = EmailTemplate.BodyMaxLength;

    /// <summary>
    /// Replaces <see cref="Body"/> once an account-link delivery reaches <see cref="DeliveryStatus.Sent"/> or
    /// <see cref="DeliveryStatus.Failed"/> (spec §3.2 step 5): the rendered link must not sit in the row any
    /// longer than sending required, but the row itself is kept (not deleted) for the admin deliveries list.
    /// </summary>
    public const string ClearedBodyMarker = "[cleared]";

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
    public string? Body { get; private set; }

    public DeliveryStatus Status { get; private set; } = DeliveryStatus.Pending;

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

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
            Subject = Truncate(subject, SubjectMaxLength),
            Body = body is null ? null : Truncate(body, BodyMaxLength),
            NextAttemptAt = now,
        };

    /// <summary>
    /// Cuts a rendered value to its column limit so one long ticket subject can never make the save throw (and
    /// poison the outbox). Never splits a surrogate pair: a dangling high surrogate is dropped with the cut.
    /// </summary>
    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var cut = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..cut];
    }

    /// <summary>Claimed by <c>EmailSender</c> (leased by <c>IEmailDeliveryClaimer</c>) and about to be handed to <c>IEmailSender</c>.</summary>
    public void MarkSending(DateTimeOffset now)
    {
        Status = DeliveryStatus.Sending;
        NextAttemptAt = now;
    }

    /// <summary>Handed to SMTP (or the pickup folder) and confirmed sent.</summary>
    public void MarkSent(DateTimeOffset now)
    {
        Status = DeliveryStatus.Sent;
        SentAt = now;
        LockedUntil = null;
    }

    /// <summary>
    /// Records one failed send attempt: still-retryable rows go back to <see cref="DeliveryStatus.Pending"/> with
    /// <see cref="NextAttemptAt"/> pushed out per <see cref="RetryBackoff"/>; the <see cref="RetryBackoff.MaxAttempts"/>-th
    /// failure moves the row to <see cref="DeliveryStatus.Failed"/> instead, visible and retryable from
    /// `/admin/email-deliveries`. <paramref name="error"/> must already be safe to store (no credentials) — callers
    /// are responsible for sanitizing it before this call.
    /// </summary>
    public void RecordFailure(string error, DateTimeOffset now)
    {
        Attempts++;
        LastError = error.Length > LastErrorMaxLength ? error[..LastErrorMaxLength] : error;
        LockedUntil = null;

        var next = RetryBackoff.NextAttemptAt(Attempts, now);
        if (next is null)
        {
            Status = DeliveryStatus.Failed;
        }
        else
        {
            Status = DeliveryStatus.Pending;
            NextAttemptAt = next.Value;
        }
    }

    /// <summary>
    /// Gives up on the delivery without a send attempt (e.g. the recipient's address changed after an account email
    /// was queued): straight to <see cref="DeliveryStatus.Failed"/>, no further retries.
    /// </summary>
    public void Abandon(string reason, DateTimeOffset now)
    {
        Status = DeliveryStatus.Failed;
        LastError = reason.Length > LastErrorMaxLength ? reason[..LastErrorMaxLength] : reason;
        LockedUntil = null;
        NextAttemptAt = now;
    }

    /// <summary>
    /// Admin retry (spec §3.2 step 4): only a <see cref="DeliveryStatus.Failed"/> delivery can be retried, reset
    /// to <see cref="DeliveryStatus.Pending"/>, due immediately, with a clean attempt count.
    /// </summary>
    public void Retry(DateTimeOffset now)
    {
        if (Status != DeliveryStatus.Failed)
        {
            throw new DomainException(ErrorCodes.EmailDeliveryNotFailed, $"Only a failed delivery can be retried; this one is {Status}.");
        }

        Status = DeliveryStatus.Pending;
        Attempts = 0;
        NextAttemptAt = now;
        LockedUntil = null;
        LastError = null;
    }

    /// <summary>See <see cref="ClearedBodyMarker"/>.</summary>
    public void ClearBody() => Body = ClearedBodyMarker;
}
