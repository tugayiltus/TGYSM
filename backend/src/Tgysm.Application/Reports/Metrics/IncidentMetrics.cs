using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>The figures of the incident report (spec §7.2). Calendar and SLA time are always labeled apart.</summary>
public static class IncidentMetrics
{
    public const string FirstResponseTime = "first_response_time";
    public const string ResolutionTime = "resolution_time";
    public const string Mttr = "mttr";
    public const string OpenBacklog = "open_backlog";
    public const string MttrCalendar = "mttr_calendar";
    public const string MttrSla = "mttr_sla";
    public const string ServiceRequestRequired = "service_request_required";

    internal static IReadOnlyList<IReportMetric> All { get; } =
    [
        new VolumeMetric(ReportName.Incidents),
        new FirstResponseTimeMetric(),
        new CompletionTimeMetric(ReportName.Incidents, ResolutionTime, TicketOutcome.Resolved),
        new MttrMetric(),
        new OpenBacklogMetric(),
        new SlaComplianceMetric(ReportName.Incidents),
        new ExcludedOutcomesMetric(
            ReportName.Incidents,
            (MetricKeys.Cancelled, t => t.Status == TicketStatus.Cancelled),
            (ServiceRequestRequired, t => t.Status == TicketStatus.Completed && t.Outcome == TicketOutcome.ServiceRequestRequired)),
    ];

    /// <summary>Calendar time is the timer's start to stop, SLA time its <c>ElapsedSeconds</c>.</summary>
    private sealed class FirstResponseTimeMetric : ReportMetric
    {
        public override string Key => FirstResponseTime;

        public override ReportName Report => ReportName.Incidents;

        public override ReportDateBasis DateBasis => ReportDateBasis.FirstResponded;

        public override MetricShape Shape => MetricShape.Duration;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context)
        {
            var responses = ReportQueries.FirstResponses(context.Db);
            return filtered.Where(t => responses.Any(i => i.TicketId == t.Id));
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var timers = ReportQueries.FirstResponses(context.Db).Where(i => filtered.Any(t => t.Id == i.TicketId));
            var sums = await DurationStats.SumAsync(timers, i => (long)(i.StoppedAt!.Value - i.StartedAt).TotalSeconds, i => i.ElapsedSeconds, cancellationToken);
            return DurationFigure.Create(Key, sums);
        }
    }

    /// <summary>
    /// Total resolution seconds ÷ resolved incidents. <c>Value</c>, <c>Numerator</c> and the <c>mttr_calendar</c> bucket are calendar
    /// seconds; the <c>mttr_sla</c> bucket is the total SLA seconds over the same denominator.
    /// </summary>
    private sealed class MttrMetric : ReportMetric
    {
        public override string Key => Mttr;

        public override ReportName Report => ReportName.Incidents;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override MetricShape Shape => MetricShape.Ratio;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.CompletedWith(TicketOutcome.Resolved));

        public override IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => Rows(filtered, context);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var sums = await TicketRules.ResolutionSumsAsync(Rows(filtered, context), context, cancellationToken);
            var ratio = ReportFigure.Ratio(Key, sums.CalendarSeconds, sums.Count, MetricKeys.UnitSeconds);
            return ratio with { Buckets = [new(MttrCalendar, null, sums.CalendarSeconds), new(MttrSla, null, sums.SlaSeconds)] };
        }
    }

    /// <summary>Open incidents now, by whole days since creation (no date window).</summary>
    private sealed class OpenBacklogMetric : ReportMetric
    {
        public override string Key => OpenBacklog;

        public override ReportName Report => ReportName.Incidents;

        public override ReportDateBasis DateBasis => ReportDateBasis.None;

        public override MetricShape Shape => MetricShape.Distribution;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.Open);

        public override IQueryable<Ticket> BucketRows(IQueryable<Ticket> filtered, ReportContext context, string bucket) =>
            InBand(Rows(filtered, context), ReportBuckets.AgeingDays.FirstOrDefault(b => b.Key == bucket) ?? throw new NotSupportedException($"Unknown bucket '{bucket}'."), context.Now);

        // Age in whole days >= from  <=>  created at or before now - from days; age <= to  <=>  created after now - (to + 1) days.
        // The first band has no lower edge: a creation time in the future (clock skew) counts as age 0.
        private static IQueryable<Ticket> InBand(IQueryable<Ticket> open, AgeBand band, DateTimeOffset now)
        {
            if (band.FromDays > 0)
            {
                var oldest = now.AddDays(-band.FromDays);
                open = open.Where(t => t.CreatedAt <= oldest);
            }

            if (band.ToDays is { } to)
            {
                var newest = now.AddDays(-(to + 1));
                open = open.Where(t => t.CreatedAt > newest);
            }

            return open;
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var open = Rows(filtered, context);
            var buckets = new List<ReportBucket>();
            foreach (var band in ReportBuckets.AgeingDays)
            {
                buckets.Add(new ReportBucket(band.Key, null, await InBand(open, band, context.Now).LongCountAsync(cancellationToken)));
            }

            return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitTickets);
        }
    }
}
