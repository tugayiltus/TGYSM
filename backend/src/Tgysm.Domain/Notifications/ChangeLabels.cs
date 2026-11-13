using Tgysm.Domain.Changes;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Notifications;

/// <summary>Turkish labels of a change's status and risk, for the server-rendered <c>status</c> and <c>risk</c> email variables.</summary>
public static class ChangeLabels
{
    private static readonly IReadOnlyDictionary<ChangeStatus, string> Statuses = new Dictionary<ChangeStatus, string>
    {
        [ChangeStatus.Draft] = "Taslak",
        [ChangeStatus.PendingApproval] = "Onay bekliyor",
        [ChangeStatus.Scheduled] = "Planlandı",
        [ChangeStatus.Implementing] = "Uygulanıyor",
        [ChangeStatus.Verifying] = "Doğrulama",
        [ChangeStatus.Closed] = "Kapalı",
        [ChangeStatus.Cancelled] = "İptal",
    };

    private static readonly IReadOnlyDictionary<Level, string> Levels = new Dictionary<Level, string>
    {
        [Level.Low] = "Düşük",
        [Level.Medium] = "Orta",
        [Level.High] = "Yüksek",
    };

    public static string Of(ChangeStatus status) => Statuses.TryGetValue(status, out var label) ? label : status.ToString();

    public static string Of(Level level) => Levels.TryGetValue(level, out var label) ? label : level.ToString();
}
