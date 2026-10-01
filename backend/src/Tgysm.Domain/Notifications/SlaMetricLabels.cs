using Tgysm.Domain.Sla;

namespace Tgysm.Domain.Notifications;

/// <summary>
/// Turkish label for each <see cref="SlaMetric"/>, written by the SLA monitor into the <c>sla_warning</c> /
/// <c>sla_breached</c> payload and rendered as the <c>slaMetric</c> email variable. Same reason as
/// <see cref="TicketStatusLabels"/>: email text is rendered server-side with no frontend to fall back on.
/// </summary>
public static class SlaMetricLabels
{
    private static readonly IReadOnlyDictionary<SlaMetric, string> Labels = new Dictionary<SlaMetric, string>
    {
        [SlaMetric.FirstResponse] = "ilk yanıt",
        [SlaMetric.Resolution] = "çözüm",
    };

    public static string Of(SlaMetric metric) =>
        Labels.TryGetValue(metric, out var label) ? label : metric.ToString();
}
