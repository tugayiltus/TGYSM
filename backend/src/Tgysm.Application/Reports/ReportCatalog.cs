using FluentValidation;
using FluentValidation.Results;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Reports;

/// <summary>All report metrics by report. Starts empty; Tasks 7–9 add their metrics to <see cref="All"/>.</summary>
public static class ReportCatalog
{
    private const string MetricField = "metric";

    private static readonly IReadOnlyList<IReportMetric> All = [];

    public static IReadOnlyList<IReportMetric> Get(ReportName report) => [.. All.Where(m => m.Report == report)];

    /// <summary>The metric of <paramref name="report"/> with this key; unknown keys are a 400 (<see cref="ErrorCodes.ReportsUnknownMetric"/>).</summary>
    public static IReportMetric Find(ReportName report, string metricKey) =>
        All.FirstOrDefault(m => m.Report == report && string.Equals(m.Key, metricKey, StringComparison.Ordinal))
            ?? throw new ValidationException([new ValidationFailure(MetricField, "Unknown metric.") { ErrorCode = ErrorCodes.ReportsUnknownMetric }]);
}
