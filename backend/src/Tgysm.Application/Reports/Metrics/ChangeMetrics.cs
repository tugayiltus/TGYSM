using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>
/// The figures of the change report (ADR-0032). A change is "closed in the period" when its status is <see cref="ChangeStatus.Closed"/>
/// and its closing time lies in the period; cancelled changes also carry a closing time but count only in <see cref="Cancelled"/>.
/// </summary>
public static class ChangeMetrics
{
    public const string ByOutcome = "by_outcome";
    public const string SuccessRate = "success_rate";
    public const string ByRisk = "by_risk";
    public const string Cancelled = MetricKeys.Cancelled;
    public const string CausedIncidents = "caused_incidents";

    private static readonly ChangeOutcome[] OutcomeOrder = [ChangeOutcome.Successful, ChangeOutcome.RolledBack, ChangeOutcome.Failed];
    private static readonly Level[] RiskOrder = [Level.Low, Level.Medium, Level.High];

    private static readonly Expression<Func<ChangeRequest, bool>> ClosedWithOutcome = c => c.Status == ChangeStatus.Closed && c.Outcome != null;
    private static readonly Expression<Func<ChangeRequest, bool>> ClosedSuccessful = c => c.Status == ChangeStatus.Closed && c.Outcome == ChangeOutcome.Successful;

    internal static IReadOnlyList<IReportMetric> All { get; } =
    [
        new EnumDistribution<ChangeOutcome>(ByOutcome, ReportDateBasis.Completed, ClosedWithOutcome, c => c.Outcome!.Value, OutcomeOrder),
        new SuccessRateMetric(),
        new EnumDistribution<Level>(ByRisk, ReportDateBasis.Created, c => true, c => c.Risk, RiskOrder),
        new CancelledMetric(),
        new CausedIncidentsMetric(),
    ];

    /// <summary>Successful changes among those closed in the period; rolled-back and failed changes only enter the denominator.</summary>
    private sealed class SuccessRateMetric : ChangeReportMetric
    {
        public override string Key => SuccessRate;

        public override ReportName Report => ReportName.Changes;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override MetricShape Shape => MetricShape.Ratio;

        public override IQueryable<ChangeRequest> Rows(IQueryable<ChangeRequest> filtered, ReportContext context) => filtered.Where(ClosedSuccessful);

        public override IQueryable<ChangeRequest>? DenominatorRows(IQueryable<ChangeRequest> filtered, ReportContext context) => filtered.Where(ClosedWithOutcome);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<ChangeRequest> filtered, ReportContext context, CancellationToken cancellationToken) =>
            ReportFigure.Ratio(
                Key,
                await Rows(filtered, context).LongCountAsync(cancellationToken),
                await DenominatorRows(filtered, context)!.LongCountAsync(cancellationToken),
                MetricKeys.UnitChanges);
    }

    private sealed class CancelledMetric : ChangeReportMetric
    {
        public override string Key => Cancelled;

        public override ReportName Report => ReportName.Changes;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override MetricShape Shape => MetricShape.Count;

        public override IQueryable<ChangeRequest> Rows(IQueryable<ChangeRequest> filtered, ReportContext context) =>
            filtered.Where(c => c.Status == ChangeStatus.Cancelled);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<ChangeRequest> filtered, ReportContext context, CancellationToken cancellationToken) =>
            new(Key, Shape, await Rows(filtered, context).LongCountAsync(cancellationToken), null, null, null, MetricKeys.UnitChanges);
    }

    /// <summary>Changes closed in the period that have at least one ticket linked as caused by them (ADR-0033); counts changes, not links.</summary>
    private sealed class CausedIncidentsMetric : ChangeReportMetric
    {
        public override string Key => CausedIncidents;

        public override ReportName Report => ReportName.Changes;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override MetricShape Shape => MetricShape.Count;

        public override IQueryable<ChangeRequest> Rows(IQueryable<ChangeRequest> filtered, ReportContext context) =>
            filtered.Where(c => c.Status == ChangeStatus.Closed
                && context.Db.ChangeTicketLinks.Any(l => l.ChangeId == c.Id && l.Kind == ChangeTicketLinkKind.Caused));

        public override async Task<ReportFigure> ComputeAsync(IQueryable<ChangeRequest> filtered, ReportContext context, CancellationToken cancellationToken) =>
            new(Key, Shape, await Rows(filtered, context).LongCountAsync(cancellationToken), null, null, null, MetricKeys.UnitChanges);
    }

    /// <summary>
    /// Changes by one enum property. Every value of <paramref name="order"/> is a bucket, empty ones included, so the report keeps one
    /// shape; the bucket key is the snake_case name and the label stays null (the UI translates it).
    /// </summary>
    private sealed class EnumDistribution<TEnum>(
        string key,
        ReportDateBasis basis,
        Expression<Func<ChangeRequest, bool>> population,
        Expression<Func<ChangeRequest, TEnum>> keyOf,
        IReadOnlyList<TEnum> order) : ChangeReportMetric
        where TEnum : struct, Enum
    {
        public override string Key => key;

        public override ReportName Report => ReportName.Changes;

        public override ReportDateBasis DateBasis => basis;

        public override MetricShape Shape => MetricShape.Distribution;

        public override IQueryable<ChangeRequest> Rows(IQueryable<ChangeRequest> filtered, ReportContext context) => filtered.Where(population);

        public override IQueryable<ChangeRequest> BucketRows(IQueryable<ChangeRequest> filtered, ReportContext context, string bucket)
        {
            var matches = order.Where(v => ReportFilter.EnumName(v) == bucket).ToList();
            if (matches.Count == 0)
            {
                throw new NotSupportedException($"Unknown bucket '{bucket}'.");
            }

            var equal = Expression.Equal(keyOf.Body, Expression.Constant(matches[0], typeof(TEnum)));
            return Rows(filtered, context).Where(Expression.Lambda<Func<ChangeRequest, bool>>(equal, keyOf.Parameters));
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<ChangeRequest> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var counts = await Rows(filtered, context)
                .GroupBy(keyOf)
                .Select(g => new { g.Key, Count = g.LongCount() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            var buckets = order.Select(v => new ReportBucket(ReportFilter.EnumName(v), null, counts.GetValueOrDefault(v))).ToList();
            return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitChanges);
        }
    }
}
