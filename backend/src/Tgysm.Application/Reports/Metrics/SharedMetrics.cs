using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>Metric keys and bucket keys used by more than one report.</summary>
public static class MetricKeys
{
    public const string Volume = "volume";
    public const string SlaCompliance = "sla_compliance";
    public const string ExcludedOutcomes = "excluded_outcomes";
    public const string Calendar = "calendar";
    public const string Sla = "sla";
    public const string Cancelled = "cancelled";

    public const string UnitTickets = "tickets";
    public const string UnitSeconds = "seconds";
}

/// <summary>Row rules shared by the metrics of the incident and request reports.</summary>
internal static class TicketRules
{
    public static readonly Expression<Func<Ticket, bool>> Completed = t => t.Status == TicketStatus.Completed;

    public static readonly Expression<Func<Ticket, bool>> Open = t => t.Status != TicketStatus.Completed && t.Status != TicketStatus.Cancelled;

    /// <summary>Completed tickets whose SLA counts: not "service request required" and not rejected.</summary>
    public static readonly Expression<Func<Ticket, bool>> SlaEligible =
        t => t.Status == TicketStatus.Completed && t.Outcome != TicketOutcome.ServiceRequestRequired && t.Outcome != TicketOutcome.ApprovalRejected;

    public static Expression<Func<Ticket, bool>> CompletedWith(TicketOutcome outcome) =>
        t => t.Status == TicketStatus.Completed && t.Outcome == outcome;

    /// <summary>Calendar seconds from creation to closing.</summary>
    public static readonly Expression<Func<Ticket, long>> CalendarSeconds = t => (long)(t.CompletedAt!.Value - t.CreatedAt).TotalSeconds;

    /// <summary>The resolution timer's SLA seconds (0 when the ticket has no timer).</summary>
    public static Expression<Func<Ticket, long>> SlaSeconds(ReportContext context)
    {
        var timers = ReportQueries.ResolutionTimers(context.Db);
        return t => timers.Where(i => i.TicketId == t.Id).Select(i => i.ElapsedSeconds).FirstOrDefault();
    }

    public static Task<DurationSums> ResolutionSumsAsync(IQueryable<Ticket> tickets, ReportContext context, CancellationToken cancellationToken) =>
        DurationStats.SumAsync(tickets, CalendarSeconds, SlaSeconds(context), cancellationToken);
}

/// <summary>Tickets created in the window.</summary>
internal sealed class VolumeMetric(ReportName report) : ReportMetric
{
    public override string Key => MetricKeys.Volume;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Created;

    public override MetricShape Shape => MetricShape.Count;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => filtered;

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
    {
        var count = await filtered.LongCountAsync(cancellationToken);
        return new ReportFigure(Key, Shape, count, null, null, null, MetricKeys.UnitTickets);
    }
}

/// <summary>Average calendar and SLA time from creation to completion of tickets completed with one outcome.</summary>
internal sealed class CompletionTimeMetric(ReportName report, string key, TicketOutcome outcome) : ReportMetric
{
    public override string Key => key;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

    public override MetricShape Shape => MetricShape.Duration;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.CompletedWith(outcome));

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
    {
        var sums = await TicketRules.ResolutionSumsAsync(Rows(filtered, context), context, cancellationToken);
        return DurationFigure.Create(Key, sums);
    }
}

internal static class DurationFigure
{
    /// <summary>Value is the average calendar seconds, <c>Denominator</c> how many were averaged; the buckets carry the rounded averages.</summary>
    public static ReportFigure Create(string key, DurationSums sums) => new(
        key,
        MetricShape.Duration,
        sums.CalendarAverage,
        null,
        sums.Count,
        sums.Count == 0 ? null : [Bucket(MetricKeys.Calendar, sums.CalendarAverage), Bucket(MetricKeys.Sla, sums.SlaAverage)],
        MetricKeys.UnitSeconds);

    private static ReportBucket Bucket(string key, double? average) => new(key, null, (long)Math.Round(average ?? 0));
}

/// <summary>Resolution timer met ÷ completed tickets whose SLA counts.</summary>
internal sealed class SlaComplianceMetric(ReportName report) : ReportMetric
{
    public override string Key => MetricKeys.SlaCompliance;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

    public override MetricShape Shape => MetricShape.Ratio;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context)
    {
        var met = ReportQueries.ResolutionTimers(context.Db).Where(i => i.Result == SlaResult.Met);
        return DenominatorRows(filtered, context)!.Where(t => met.Any(i => i.TicketId == t.Id));
    }

    public override IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.SlaEligible);

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken) =>
        ReportFigure.Ratio(Key, await Rows(filtered, context).LongCountAsync(cancellationToken), await DenominatorRows(filtered, context)!.LongCountAsync(cancellationToken));
}

/// <summary>Tickets closed without counting toward SLA, split into two disjoint groups.</summary>
internal sealed class ExcludedOutcomesMetric(
    ReportName report,
    (string Key, Expression<Func<Ticket, bool>> Rule) first,
    (string Key, Expression<Func<Ticket, bool>> Rule) second) : ReportMetric
{
    public override string Key => MetricKeys.ExcludedOutcomes;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

    public override MetricShape Shape => MetricShape.Distribution;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) =>
        filtered.Where(Or(first.Rule, second.Rule));

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
    {
        var firstCount = await filtered.Where(first.Rule).LongCountAsync(cancellationToken);
        var secondCount = await filtered.Where(second.Rule).LongCountAsync(cancellationToken);
        return new ReportFigure(
            Key, Shape, firstCount + secondCount, null, null, [new(first.Key, null, firstCount), new(second.Key, null, secondCount)], MetricKeys.UnitTickets);
    }

    private static Expression<Func<Ticket, bool>> Or(Expression<Func<Ticket, bool>> left, Expression<Func<Ticket, bool>> right)
    {
        var ticket = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], ticket).Visit(right.Body);
        return Expression.Lambda<Func<Ticket, bool>>(Expression.OrElse(left.Body, rightBody), ticket);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
