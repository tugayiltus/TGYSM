namespace Tgysm.Domain.Notifications;

/// <summary>
/// Fixed `{{name}}` variables available to email templates (spec §3.4), scoped per <see cref="NotificationEventTypes"/>.
/// Saving a template with a name outside <see cref="AllVariables"/> fails with `email_templates.unknown_variable`;
/// a known name not allowed for that event fails with `email_templates.variable_not_allowed` (ADR-0016).
/// </summary>
public static class TemplateVariables
{
    public const string TicketNumber = "ticketNumber";
    public const string Subject = "subject";
    public const string Status = "status";
    public const string Resolution = "resolution";
    public const string CompletedAt = "completedAt";
    public const string TicketUrl = "ticketUrl";
    public const string RecipientName = "recipientName";
    public const string ActionUrl = "actionUrl";
    public const string ExpiresAt = "expiresAt";
    public const string SlaMetric = "slaMetric";
    public const string DueAt = "dueAt";

    private static readonly IReadOnlySet<string> TicketCommon = new HashSet<string>(StringComparer.Ordinal)
    {
        TicketNumber, Subject, Status, TicketUrl, RecipientName,
    };

    private static readonly IReadOnlySet<string> TicketCompletedSet = Union(TicketCommon, Resolution, CompletedAt);

    private static readonly IReadOnlySet<string> SlaSet = Union(TicketCommon, SlaMetric, DueAt);

    private static readonly IReadOnlySet<string> AccountSet = new HashSet<string>(StringComparer.Ordinal)
    {
        RecipientName, ActionUrl, ExpiresAt,
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedByEvent = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        [NotificationEventTypes.TicketCreated] = TicketCommon,
        [NotificationEventTypes.TicketAssigned] = TicketCommon,
        [NotificationEventTypes.PublicReply] = TicketCommon,
        [NotificationEventTypes.ApprovalRequested] = TicketCommon,
        [NotificationEventTypes.ApprovalDecided] = TicketCommon,
        [NotificationEventTypes.TicketCompleted] = TicketCompletedSet,
        [NotificationEventTypes.SlaWarning] = SlaSet,
        [NotificationEventTypes.SlaBreached] = SlaSet,
        [NotificationEventTypes.AccountActivation] = AccountSet,
        [NotificationEventTypes.PasswordReset] = AccountSet,
    };

    /// <summary>Every variable name recognized anywhere, regardless of event; a name outside this set is unknown.</summary>
    public static readonly IReadOnlySet<string> AllVariables = new HashSet<string>(StringComparer.Ordinal)
    {
        TicketNumber, Subject, Status, Resolution, CompletedAt, TicketUrl, RecipientName, ActionUrl, ExpiresAt, SlaMetric, DueAt,
    };

    /// <summary>The variable names an admin may use for <paramref name="eventType"/> (one of <see cref="NotificationEventTypes"/>).</summary>
    public static IReadOnlySet<string> AllowedFor(string eventType) =>
        AllowedByEvent.TryGetValue(eventType, out var allowed)
            ? allowed
            : throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unknown notification event type.");

    private static IReadOnlySet<string> Union(IReadOnlySet<string> baseSet, params string[] extra)
    {
        var set = new HashSet<string>(baseSet, StringComparer.Ordinal);
        foreach (var item in extra)
        {
            set.Add(item);
        }

        return set;
    }
}
