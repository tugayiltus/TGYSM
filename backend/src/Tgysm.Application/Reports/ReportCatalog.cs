using FluentValidation;
using FluentValidation.Results;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>All report metrics by report.</summary>
public static class ReportCatalog
{
    private const string MetricField = "metric";

    private static readonly IReadOnlyList<IReportMetric> All = [.. IncidentMetrics.All, .. RequestMetrics.All, .. OperationsMetrics.All, .. TransferMetrics.All];

    /// <summary>The filter a report really runs with: the incident and request reports fix the ticket kind themselves, so a <c>Kind</c> filter is ignored there.</summary>
    public static ReportFilter EffectiveFilter(ReportName report, ReportFilter filter) => report switch
    {
        ReportName.Incidents => filter with { Kind = TicketKind.Incident },
        ReportName.Requests => filter with { Kind = TicketKind.Request },
        _ => filter,
    };

    public static IReadOnlyList<IReportMetric> Get(ReportName report) => [.. All.Where(m => m.Report == report)];

    /// <summary>The metric of <paramref name="report"/> with this key; unknown keys are a 400 (<see cref="ErrorCodes.ReportsUnknownMetric"/>).</summary>
    public static IReportMetric Find(ReportName report, string metricKey) =>
        All.FirstOrDefault(m => m.Report == report && string.Equals(m.Key, metricKey, StringComparison.Ordinal))
            ?? throw new ValidationException([new ValidationFailure(MetricField, "Unknown metric.") { ErrorCode = ErrorCodes.ReportsUnknownMetric }]);
}
