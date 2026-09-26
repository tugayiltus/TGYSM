using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

internal static class TicketQueries
{
    private const string NameSeparator = " ";

    /// <summary>Latest approval (pending, or the last decision) of a ticket.</summary>
    public static Task<ApprovalSummary?> LatestApprovalAsync(IAppDbContext db, Guid ticketId, CancellationToken cancellationToken) =>
        db.Approvals.AsNoTracking().Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new ApprovalSummary(
                a.Id, a.Version, a.Status, a.ApproverId,
                db.Users.Where(u => u.Id == a.ApproverId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                a.Reason, a.CreatedAt, a.DecidedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task<TicketDetail> LoadDetailAsync(IAppDbContext db, Guid ticketId, TicketViewer viewer, CancellationToken cancellationToken)
    {
        var t = await db.Tickets.AsNoTracking().SingleAsync(x => x.Id == ticketId, cancellationToken);
        var userIds = new[] { t.RequesterId, t.AssigneeId ?? t.RequesterId };
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + NameSeparator + u.LastName, cancellationToken);
        string? UserName(Guid? id) => id is { } key ? names.GetValueOrDefault(key) : null;
        var category = t.CategoryId is null ? null : await db.Categories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken);
        var location = t.LocationId is null ? null : await db.Locations.Where(l => l.Id == t.LocationId).Select(l => l.Name).FirstOrDefaultAsync(cancellationToken);
        var team = await db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).SingleAsync(cancellationToken);
        var requestType = t.RequestTypeId is null ? null : await db.RequestTypes.Where(r => r.Id == t.RequestTypeId).Select(r => r.Name).FirstOrDefaultAsync(cancellationToken);
        var approval = await LatestApprovalAsync(db, t.Id, cancellationToken);
        var relatedIds = await db.TicketRelations.AsNoTracking()
            .Where(r => r.IncidentId == t.Id || r.RequestId == t.Id)
            .Select(r => r.IncidentId == t.Id ? r.RequestId : r.IncidentId)
            .ToListAsync(cancellationToken);
        var related = await VisibleTicketsAsync(db, relatedIds, viewer, cancellationToken);

        return new TicketDetail(
            t.Id, t.Number, t.Kind, t.Subject, t.Description, t.Status, t.HoldReason, t.HoldNote, t.Impact, t.Urgency, t.Priority,
            t.CategoryId, category, t.LocationId, location, t.RequesterId, UserName(t.RequesterId)!, t.TeamId, team, t.AssigneeId, UserName(t.AssigneeId),
            t.RequestTypeId, requestType, t.RequiresApproval,
            viewer.SeesInternal ? t.RootCause : null,
            t.Resolution, t.Outcome, t.CancelReason, t.CreatedAt, t.UpdatedAt, t.CompletedAt, approval, related, viewer.CanWork(t), t.Version);
    }

    /// <summary>Tickets by id, keeping only those the viewer may see (relations and asset links respect scope).</summary>
    public static async Task<IReadOnlyList<RelatedTicketItem>> VisibleTicketsAsync(IAppDbContext db, IReadOnlyCollection<Guid> ids, TicketViewer viewer, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var tickets = await db.Tickets.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        return [.. tickets.Where(viewer.CanSee).Select(x => new RelatedTicketItem(x.Number, x.Kind, x.Subject, x.Status))];
    }
}
