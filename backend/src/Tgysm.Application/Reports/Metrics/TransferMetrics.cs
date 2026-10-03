using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>
/// The figures of the transfers report (spec §7.2). They are dated by the assignment history (or relation) row, not by the ticket, and
/// scoped by the ticket: only rows of tickets the viewer may see count. A row's drill-down entry is its ticket, repeated once per row.
/// </summary>
public static class TransferMetrics
{
    public const string TransferCount = "transfer_count";
    public const string TransferRate = "transfer_rate";
    public const string Reassignments = "reassignments";
    public const string IncidentToRequest = "incident_to_request";

    internal static IReadOnlyList<IReportMetric> All { get; } =
    [
        new AssignmentCountMetric(TransferCount, a => a.FromTeamId != a.ToTeamId),
        new TransferRateMetric(),
        new AssignmentCountMetric(Reassignments, a => a.FromTeamId == a.ToTeamId && a.FromAssigneeId != null && a.FromAssigneeId != a.ToAssigneeId),
        new IncidentToRequestMetric(),
    ];

    private static IQueryable<TicketAssignment> InWindow(IQueryable<TicketAssignment> rows, ReportWindow window)
    {
        if (window.FromInclusive is { } from)
        {
            rows = rows.Where(a => a.OccurredAt >= from);
        }

        return window.ToExclusive is { } to ? rows.Where(a => a.OccurredAt < to) : rows;
    }

    private static IQueryable<Ticket> TicketsOf(IQueryable<Ticket> filtered, IQueryable<TicketAssignment> rows) =>
        from t in filtered
        from a in rows
        where a.TicketId == t.Id
        select t;

    /// <summary>Assignment rows in the window that match a rule, one per row.</summary>
    private sealed class AssignmentCountMetric(string key, System.Linq.Expressions.Expression<Func<TicketAssignment, bool>> rule) : ReportMetric
    {
        public override string Key => key;

        public override ReportName Report => ReportName.Transfers;

        public override ReportDateBasis DateBasis => ReportDateBasis.None;

        public override MetricShape Shape => MetricShape.Count;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) =>
            TicketsOf(filtered, InWindow(context.Db.TicketAssignments.Where(rule), context.Window));

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken) =>
            new(Key, Shape, await Rows(filtered, context).LongCountAsync(cancellationToken), null, null, null, MetricKeys.UnitTickets);
    }

    /// <summary>Tickets created in the window with at least one transfer in the window ÷ tickets created in the window.</summary>
    private sealed class TransferRateMetric : ReportMetric
    {
        public override string Key => TransferRate;

        public override ReportName Report => ReportName.Transfers;

        public override ReportDateBasis DateBasis => ReportDateBasis.Created;

        public override MetricShape Shape => MetricShape.Ratio;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context)
        {
            var transfers = InWindow(context.Db.TicketAssignments.Where(a => a.FromTeamId != a.ToTeamId), context.Window);
            return filtered.Where(t => transfers.Any(a => a.TicketId == t.Id));
        }

        public override IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => filtered;

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken) =>
            ReportFigure.Ratio(Key, await Rows(filtered, context).LongCountAsync(cancellationToken), await filtered.LongCountAsync(cancellationToken));
    }

    /// <summary>Incident-to-request relations created in the window whose incident is in scope; each relation counts once.</summary>
    private sealed class IncidentToRequestMetric : ReportMetric
    {
        public override string Key => IncidentToRequest;

        public override ReportName Report => ReportName.Transfers;

        public override ReportDateBasis DateBasis => ReportDateBasis.None;

        public override MetricShape Shape => MetricShape.Count;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context)
        {
            var relations = context.Db.TicketRelations.AsQueryable();
            if (context.Window.FromInclusive is { } from)
            {
                relations = relations.Where(r => r.CreatedAt >= from);
            }

            if (context.Window.ToExclusive is { } to)
            {
                relations = relations.Where(r => r.CreatedAt < to);
            }

            return from t in filtered
                   from r in relations
                   where r.IncidentId == t.Id
                   select t;
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken) =>
            new(Key, Shape, await Rows(filtered, context).LongCountAsync(cancellationToken), null, null, null, MetricKeys.UnitTickets);
    }
}
