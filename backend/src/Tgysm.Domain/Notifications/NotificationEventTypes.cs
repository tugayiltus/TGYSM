namespace Tgysm.Domain.Notifications;

/// <summary>
/// Notification event type codes written to the outbox (spec §3.3). Every constant needs a Turkish label under
/// "emailEvents" in frontend/src/shared/i18n/locales/tr/admin.json (checked by TranslationCoverageTests).
/// </summary>
public static class NotificationEventTypes
{
    public const string TicketCreated = "ticket_created";
    public const string TicketAssigned = "ticket_assigned";
    public const string PublicReply = "public_reply";
    public const string ApprovalRequested = "approval_requested";
    public const string ApprovalDecided = "approval_decided";
    public const string TicketCompleted = "ticket_completed";
    public const string SlaWarning = "sla_warning";
    public const string SlaBreached = "sla_breached";
    public const string AccountActivation = "account_activation";
    public const string PasswordReset = "password_reset";
}
