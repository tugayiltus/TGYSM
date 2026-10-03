using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>The figures of the request report (spec §7.2).</summary>
public static class RequestMetrics
{
    public const string FulfilmentTime = "fulfilment_time";
    public const string ApprovalWait = "approval_wait";
    public const string RejectionRate = "rejection_rate";
    public const string ApprovalRejected = "approval_rejected";

    internal static IReadOnlyList<IReportMetric> All { get; } =
    [
        new VolumeMetric(ReportName.Requests),
        new CompletionTimeMetric(ReportName.Requests, FulfilmentTime, TicketOutcome.Fulfilled),
        new ApprovalWaitMetric(),
        new RejectionRateMetric(),
        new SlaComplianceMetric(ReportName.Requests),
        new ExcludedOutcomesMetric(
            ReportName.Requests,
            (MetricKeys.Cancelled, t => t.Status == TicketStatus.Cancelled && t.Outcome != TicketOutcome.ApprovalRejected),
            (ApprovalRejected, t => t.Outcome == TicketOutcome.ApprovalRejected)),
    ];

    /// <summary>Average <c>DecidedAt − CreatedAt</c> of approved or rejected approvals decided in the window (one per request).</summary>
    private sealed class ApprovalWaitMetric : ReportMetric
    {
        public override string Key => ApprovalWait;

        public override ReportName Report => ReportName.Requests;

        public override ReportDateBasis DateBasis => ReportDateBasis.Decided;

        public override MetricShape Shape => MetricShape.Duration;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context)
        {
            var decided = ReportQueries.DecidedApprovals(context.Db);
            return filtered.Where(t => decided.Any(a => a.TicketId == t.Id));
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var seconds = ReportQueries.DecidedApprovals(context.Db)
                .Where(a => filtered.Any(t => t.Id == a.TicketId))
                .Select(a => (long)(a.DecidedAt!.Value - a.CreatedAt).TotalSeconds);
            var (average, count) = await DurationStats.AverageAsync(seconds, cancellationToken);
            return new ReportFigure(Key, Shape, average, null, count, null, MetricKeys.UnitSeconds);
        }
    }

    /// <summary>Requests rejected at approval ÷ requests that ended in the window by completion or rejection (a plain cancellation is apart).</summary>
    private sealed class RejectionRateMetric : ReportMetric
    {
        public override string Key => RejectionRate;

        public override ReportName Report => ReportName.Requests;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override MetricShape Shape => MetricShape.Ratio;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) =>
            filtered.Where(t => t.Outcome == TicketOutcome.ApprovalRejected);

        public override IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) =>
            filtered.Where(t => t.Status == TicketStatus.Completed || t.Outcome == TicketOutcome.ApprovalRejected);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken) =>
            ReportFigure.Ratio(Key, await Rows(filtered, context).LongCountAsync(cancellationToken), await DenominatorRows(filtered, context)!.LongCountAsync(cancellationToken));
    }
}
