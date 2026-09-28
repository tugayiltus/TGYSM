using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Notifications;

/// <summary>
/// Turkish label for each <see cref="TicketStatus"/> — the single source <c>NotificationContextLoader</c> uses to
/// fill the <c>status</c> email variable (spec §3.4). Email is rendered entirely server-side with no frontend to
/// fall back on, unlike UI text (ADR-0006 keeps that in <c>frontend/src/shared/i18n</c>), so this narrow case needs
/// its own backend-only Turkish map instead.
/// </summary>
public static class TicketStatusLabels
{
    private static readonly IReadOnlyDictionary<TicketStatus, string> Labels = new Dictionary<TicketStatus, string>
    {
        [TicketStatus.Open] = "Açık",
        [TicketStatus.InProgress] = "İşlemde",
        [TicketStatus.OnHold] = "Beklemede",
        [TicketStatus.Completed] = "Tamamlandı",
        [TicketStatus.Cancelled] = "İptal edildi",
    };

    /// <summary>Falls back to the raw enum name only if a future <see cref="TicketStatus"/> value is added here late; covered by a completeness test.</summary>
    public static string Of(TicketStatus status) =>
        Labels.TryGetValue(status, out var label) ? label : status.ToString();
}
