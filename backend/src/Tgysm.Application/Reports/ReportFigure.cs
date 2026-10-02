namespace Tgysm.Application.Reports;

public enum MetricShape
{
    Count,
    Ratio,
    Duration,
    Distribution,
}

public sealed record ReportBucket(string Key, string? Label, long Count);

/// <summary>
/// One computed number. Ratios carry numerator and denominator; a zero denominator gives a null <c>Value</c> (the UI shows "Veri yok").
/// </summary>
public sealed record ReportFigure(
    string Metric, MetricShape Shape, double? Value, long? Numerator, long? Denominator, IReadOnlyList<ReportBucket>? Buckets, string? Unit)
{
    /// <summary>A ratio figure; <paramref name="denominator"/> 0 gives a null value.</summary>
    public static ReportFigure Ratio(string metric, long numerator, long denominator, string? unit = null) =>
        new(metric, MetricShape.Ratio, denominator == 0 ? null : (double)numerator / denominator, numerator, denominator, null, unit);
}
