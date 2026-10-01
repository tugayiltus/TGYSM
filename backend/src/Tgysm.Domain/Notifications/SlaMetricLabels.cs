using Tgysm.Domain.Sla;

namespace Tgysm.Domain.Notifications;

/// <summary>
/// Payload key and Turkish label of each <see cref="SlaMetric"/>. The SLA monitor writes only the key
/// (<see cref="KeyOf"/>) into the <c>sla_warning</c>/<c>sla_breached</c> payload (ids and keys only);
/// <c>NotificationContextLoader</c> maps it to the label (<see cref="LabelOfKey"/>) at render time for the
/// <c>slaMetric</c> email variable. Same reason as <see cref="TicketStatusLabels"/>: email text is rendered
/// server-side with no frontend to fall back on.
/// </summary>
public static class SlaMetricLabels
{
    public const string FirstResponseKey = "first_response";
    public const string ResolutionKey = "resolution";

    private static readonly IReadOnlyDictionary<SlaMetric, string> Keys = new Dictionary<SlaMetric, string>
    {
        [SlaMetric.FirstResponse] = FirstResponseKey,
        [SlaMetric.Resolution] = ResolutionKey,
    };

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [FirstResponseKey] = "ilk yanıt",
        [ResolutionKey] = "çözüm",
    };

    public static string KeyOf(SlaMetric metric) => Keys[metric];

    /// <summary>Falls back to the raw key for an unknown one.</summary>
    public static string LabelOfKey(string key) => Labels.TryGetValue(key, out var label) ? label : key;
}
