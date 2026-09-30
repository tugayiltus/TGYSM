namespace Tgysm.Domain.Notifications;

/// <summary>
/// Outbox event types the worker handles itself and never emails: no template, no recipients, no Turkish label.
/// Kept out of <see cref="NotificationEventTypes"/> on purpose, so every map that enumerates emailable events
/// (<see cref="TemplateVariables"/>, the seeded templates, the <c>emailEvents</c> translations) excludes them.
/// </summary>
public static class InternalEventTypes
{
    /// <summary>
    /// A forgot-password request (ADR-0017 amendment 2026-10-01): the request path only records the normalized
    /// email under <see cref="OutboxEvent.RequestedEmailKey"/>, so it does the same work whether or not the account
    /// exists; the dispatcher looks the account up and issues the reset link for an active one.
    /// </summary>
    public const string PasswordResetRequested = "password_reset_requested";
}
