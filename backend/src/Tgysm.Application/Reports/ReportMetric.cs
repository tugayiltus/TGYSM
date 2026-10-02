using Tgysm.Application.Abstractions;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>What a ticket metric needs besides the filtered tickets: the database (SLA instances, approvals) and the current instant.</summary>
public sealed record ReportContext(IAppDbContext Db, DateTimeOffset Now);

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

    /// <summary>Rows behind a ratio's denominator; null for other shapes.</summary>
    public virtual IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => null;

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken);
}

/// <summary>A metric over assets (asset and warranty reports).</summary>
public abstract class AssetReportMetric : IReportMetric
{
    public abstract string Key { get; }

    public abstract ReportName Report { get; }

    public virtual ReportDateBasis DateBasis => ReportDateBasis.Created;

    public abstract MetricShape Shape { get; }

    public abstract IQueryable<Asset> Rows(IQueryable<Asset> filtered);

    public virtual IQueryable<Asset>? DenominatorRows(IQueryable<Asset> filtered) => null;

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<Asset> filtered, CancellationToken cancellationToken);
}
