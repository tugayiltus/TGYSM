using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>Applies a <see cref="ReportFilter"/> to tickets: every non-date filter plus the day window on the metric's date basis.</summary>
public sealed class TicketReportSource(IAppDbContext db, ICompanyClock clock)
{
    public IQueryable<Ticket> Apply(IQueryable<Ticket> tickets, ReportFilter filter, ReportDateBasis basis)
    {
        var window = filter.Window(clock.Zone);
        if (window.FromInclusive is { } from)
        {
            tickets = basis == ReportDateBasis.Created ? tickets.Where(t => t.CreatedAt >= from) : tickets.Where(t => t.CompletedAt >= from);
        }

        if (window.ToExclusive is { } to)
        {
            tickets = basis == ReportDateBasis.Created ? tickets.Where(t => t.CreatedAt < to) : tickets.Where(t => t.CompletedAt < to);
        }

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
