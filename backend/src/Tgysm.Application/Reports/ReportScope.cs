using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Reports;

/// <summary>Who runs a report: an admin or IT staff member with their team ids.</summary>
public sealed record ReportViewer(Guid UserId, UserRole Role, IReadOnlyList<Guid> TeamIds);

/// <summary>
/// Report access (spec §7.1): admins see all tickets; IT staff the tickets whose current team is one of theirs (not
/// tickets they only requested); employees are forbidden. Asset reports: IT staff and admins, all assets.
/// </summary>
public sealed class ReportScope(IAppDbContext db, ICurrentActor actor)
{
    public Task<ReportViewer> ViewerAsync(CancellationToken cancellationToken) =>
        ViewerForAsync(actor.UserId ?? throw new AuthenticationFailedException(), cancellationToken);

    /// <summary>The viewer for a given user (export jobs run as their creator); unknown, inactive or employee users are forbidden.</summary>
    public async Task<ReportViewer> ViewerForAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Role, u.IsActive }).SingleOrDefaultAsync(cancellationToken);
        if (user is not { IsActive: true } || user.Role == UserRole.Employee)
        {
            throw new ForbiddenException();
        }

        var teams = await db.TeamMemberships.Where(m => m.UserId == userId).Select(m => m.TeamId).ToListAsync(cancellationToken);
        return new ReportViewer(userId, user.Role, teams);
    }

    public async Task<IQueryable<Ticket>> TicketsAsync(CancellationToken cancellationToken) => TicketsFor(await ViewerAsync(cancellationToken));

    public async Task<IQueryable<Asset>> AssetsAsync(CancellationToken cancellationToken) => AssetsFor(await ViewerAsync(cancellationToken));

    public IQueryable<Ticket> TicketsFor(ReportViewer viewer)
    {
        var teamIds = viewer.TeamIds;
        return viewer.Role switch
        {
            UserRole.Admin => db.Tickets,
            UserRole.ItStaff => db.Tickets.Where(t => teamIds.Contains(t.TeamId)),
            _ => throw new ForbiddenException(),
        };
    }

    public IQueryable<Asset> AssetsFor(ReportViewer viewer) =>
        viewer.Role == UserRole.Employee ? throw new ForbiddenException() : db.Assets;
}
