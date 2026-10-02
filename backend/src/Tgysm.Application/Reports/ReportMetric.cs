using Tgysm.Application.Abstractions;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>
/// What a ticket metric needs besides the filtered tickets: the database (SLA instances, approvals), the current instant and the
/// report's day window, for metrics whose dated rows are not tickets (assignment history, ticket relations).
/// </summary>
public sealed record ReportContext(IAppDbContext Db, DateTimeOffset Now, ReportWindow Window);

/// <summary>What every metric states about itself, ticket-based or asset-based.</summary>
public interface IReportMetric
{
    string Key { get; }

    ReportName Report { get; }

    ReportDateBasis DateBasis { get; }

    MetricShape Shape { get; }
}

/// <summary>A metric over tickets. <see cref="Rows"/> are the drill-down/export rows (a ratio's numerator rows).</summary>
public abstract class ReportMetric : IReportMetric
{
    public abstract string Key { get; }

    public abstract ReportName Report { get; }

    public abstract ReportDateBasis DateBasis { get; }

    public abstract MetricShape Shape { get; }

    public abstract IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context);

    /// <summary>The <see cref="Rows"/> of one bucket of a distribution; its count equals the bucket's count. Only distributions have buckets.</summary>
    public virtual IQueryable<Ticket> BucketRows(IQueryable<Ticket> filtered, ReportContext context, string bucket) =>
        throw new NotSupportedException($"Metric '{Key}' has no buckets.");

    /// <summary>Rows behind a ratio's denominator; null for other shapes.</summary>
    public virtual IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => null;

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken);
}

/// <summary>
/// What an asset metric needs besides the filtered assets: the database (assignments), today in the company zone and the warranty
/// window (<see cref="WarrantyFrom"/>..<see cref="WarrantyTo"/>, inclusive).
/// </summary>
public sealed record AssetReportContext(IAppDbContext Db, DateOnly Today, DateOnly WarrantyFrom, DateOnly WarrantyTo)
{
    /// <summary>
    /// The context of a run at <paramref name="now"/>. The warranty window defaults to today..today + <see cref="ReportBuckets.DefaultWarrantyWindowDays"/>;
    /// its start never lies before today, so "expiring" and "expired" never share an asset.
    /// </summary>
    public static AssetReportContext Create(IAppDbContext db, ReportFilter filter, DateTimeOffset now, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var from = filter.From is { } start && start > today ? start : today;
        var to = filter.To ?? today.AddDays(ReportBuckets.DefaultWarrantyWindowDays);
        return new AssetReportContext(db, today, from, to);
    }
}

/// <summary>A metric over assets (asset and warranty reports).</summary>
public abstract class AssetReportMetric : IReportMetric
{
    public abstract string Key { get; }

    public abstract ReportName Report { get; }

    public virtual ReportDateBasis DateBasis => ReportDateBasis.Created;

    public abstract MetricShape Shape { get; }

    public abstract IQueryable<Asset> Rows(IQueryable<Asset> filtered, AssetReportContext context);

    /// <summary>The <see cref="Rows"/> of one bucket of a distribution; its count equals the bucket's count. Only distributions have buckets.</summary>
    public virtual IQueryable<Asset> BucketRows(IQueryable<Asset> filtered, AssetReportContext context, string bucket) =>
        throw new NotSupportedException($"Metric '{Key}' has no buckets.");

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<Asset> filtered, AssetReportContext context, CancellationToken cancellationToken);
}
