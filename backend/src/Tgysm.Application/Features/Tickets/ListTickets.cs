using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Sla;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

public enum TicketQueue
{
    Mine,
    Team,
    AssignedToMe,
    Unassigned,
    OnHold,
    SlaRisk,
}

public sealed record ListTicketsQuery(TicketQueue? Queue, TicketStatus? Status, TicketKind? Kind, Priority? Priority, string? Search, int Page, int PageSize);

/// <summary>Lists tickets in the viewer's scope; queues and filters only narrow it.</summary>
public sealed class ListTicketsHandler(IAppDbContext db, TicketScope scope, TicketSlaReader slaReader)
{
    private const string NameSeparator = " ";

    public async Task<PagedResult<TicketListItem>> HandleAsync(ListTicketsQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.ViewerAsync(cancellationToken);
        if (query.Queue == TicketQueue.SlaRisk && !viewer.SeesInternal)
        {
            throw new ForbiddenException();
        }

        var tickets = scope.Visible(viewer).AsNoTracking();
        var teamIds = viewer.TeamIds;
        tickets = query.Queue switch
        {
            TicketQueue.Mine => tickets.Where(t => t.RequesterId == viewer.UserId),
            TicketQueue.Team => tickets.Where(t => teamIds.Contains(t.TeamId)),
            TicketQueue.AssignedToMe => tickets.Where(t => t.AssigneeId == viewer.UserId),
            TicketQueue.Unassigned => tickets.Where(t => t.AssigneeId == null && t.Status != TicketStatus.Completed && t.Status != TicketStatus.Cancelled),
            TicketQueue.OnHold => tickets.Where(t => t.Status == TicketStatus.OnHold),
            TicketQueue.SlaRisk => tickets.Where(t => db.SlaInstances.Any(i => i.TicketId == t.Id && i.StoppedAt == null && i.RunningSince != null && (i.WarnedAt != null || i.BreachedAt != null))),
            _ => tickets,
        };

        if (query.Status is { } status)
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (query.Kind is { } kind)
        {
            tickets = tickets.Where(t => t.Kind == kind);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var number = TicketNumber.Normalize(query.Search);
            var key = TurkishText.ToSearchKey(query.Search);
            tickets = TicketNumber.IsValid(number)
                ? tickets.Where(t => t.Number == number)
                : tickets.Where(t => t.SearchText.Contains(key));
        }

        var page = await tickets
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Select(t => new TicketListItem(
                t.Id, t.Number, t.Kind, t.Subject, t.Status, t.HoldReason, t.Priority,
                db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                db.Users.Where(u => u.Id == t.RequesterId).Select(u => u.FirstName + NameSeparator + u.LastName).First(),
                t.CreatedAt, t.UpdatedAt, Array.Empty<TicketSla>()))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);

        var sla = await slaReader.LoadAsync([.. page.Items.Select(i => i.Id)], cancellationToken);
        return page with { Items = [.. page.Items.Select(i => i with { Sla = sla.GetValueOrDefault(i.Id) ?? [] })] };
    }
}
