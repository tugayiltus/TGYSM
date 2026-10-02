using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>Applies a <see cref="ReportFilter"/> to tickets: every non-date filter plus the day window on the metric's date basis.</summary>
public sealed class TicketReportSource(IAppDbContext db, ICompanyClock clock)
{
    /// <summary>The filter's day window in company time, for metrics that date rows other than tickets.</summary>
    public ReportWindow Window(ReportFilter filter) => filter.Window(clock.Zone);

    public IQueryable<Ticket> Apply(IQueryable<Ticket> tickets, ReportFilter filter, ReportDateBasis basis)
    {
        tickets = InWindow(tickets, filter.Window(clock.Zone), basis);

        if (filter.Kind is { } kind)
        {
            tickets = tickets.Where(t => t.Kind == kind);
        }

        if (filter.Priority is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        if (filter.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(t => t.CategoryId == categoryId);
        }

        if (filter.TeamId is { } teamId)
        {
            tickets = tickets.Where(t => t.TeamId == teamId);
        }

        if (filter.AssigneeId is { } assigneeId)
        {
            tickets = tickets.Where(t => t.AssigneeId == assigneeId);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            tickets = tickets.Where(t => db.Users.Any(u => u.Id == t.RequesterId && u.DepartmentId == departmentId));
        }

        if (filter.LocationId is { } locationId)
        {
            tickets = tickets.Where(t => t.LocationId == locationId);
        }

        if (filter.Status is { } status)
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        return filter.SlaState is { } state ? WithSlaState(tickets, state) : tickets;
    }

    private IQueryable<Ticket> InWindow(IQueryable<Ticket> tickets, ReportWindow window, ReportDateBasis basis)
    {
        var (from, to) = (window.FromInclusive, window.ToExclusive);
        switch (basis)
        {
            case ReportDateBasis.None:
                return tickets;
            case ReportDateBasis.FirstResponded:
                var responses = ReportQueries.FirstResponses(db);
                responses = from is { } f ? responses.Where(i => i.StoppedAt >= f) : responses;
                responses = to is { } u ? responses.Where(i => i.StoppedAt < u) : responses;
                return tickets.Where(t => responses.Any(i => i.TicketId == t.Id));
            case ReportDateBasis.Decided:
                var decisions = ReportQueries.DecidedApprovals(db);
                decisions = from is { } d ? decisions.Where(a => a.DecidedAt >= d) : decisions;
                decisions = to is { } e ? decisions.Where(a => a.DecidedAt < e) : decisions;
                return tickets.Where(t => decisions.Any(a => a.TicketId == t.Id));
            default:
                if (from is { } start)
                {
                    tickets = basis == ReportDateBasis.Created ? tickets.Where(t => t.CreatedAt >= start) : tickets.Where(t => t.CompletedAt >= start);
                }

                if (to is { } end)
                {
                    tickets = basis == ReportDateBasis.Created ? tickets.Where(t => t.CreatedAt < end) : tickets.Where(t => t.CompletedAt < end);
                }

                return tickets;
        }
    }

    // Resolution timer only (spec §7.1).
    private IQueryable<Ticket> WithSlaState(IQueryable<Ticket> tickets, SlaStateFilter state)
    {
        var instances = db.SlaInstances.Where(i => i.Metric == SlaMetric.Resolution);
        return state switch
        {
            SlaStateFilter.Met => tickets.Where(t => instances.Any(i => i.TicketId == t.Id && i.Result == SlaResult.Met)),
            SlaStateFilter.Breached => tickets.Where(t => instances.Any(i => i.TicketId == t.Id && i.Result == SlaResult.Breached)),
            SlaStateFilter.OpenBreached => tickets.Where(t => instances.Any(i => i.TicketId == t.Id && i.StoppedAt == null && i.BreachedAt != null)),
            _ => tickets.Where(t => instances.Any(i => i.TicketId == t.Id && i.StoppedAt == null && i.WarnedAt != null && i.BreachedAt == null)),
        };
    }
}
