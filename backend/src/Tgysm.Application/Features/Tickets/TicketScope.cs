using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Tickets;

/// <summary>The signed-in user as ticket access sees them (ADR-0013).</summary>
public sealed record TicketViewer(Guid UserId, UserRole Role, IReadOnlyList<Guid> TeamIds)
{
    /// <summary>IT staff and admins see internal notes, files and fields.</summary>
    public bool SeesInternal => Role != UserRole.Employee;

    /// <summary>May work on the ticket: admin always, IT staff for tickets of their teams.</summary>
    public bool CanWork(Ticket ticket) => Role == UserRole.Admin || (Role == UserRole.ItStaff && TeamIds.Contains(ticket.TeamId));
}

/// <summary>
/// Access scope (spec §3.6): employees see their own tickets; IT staff their own plus their teams'; admins all.
/// Everything outside scope is "not found", so numbers do not leak.
/// </summary>
public sealed class TicketScope(IAppDbContext db, ICurrentActor actor)
{
    public async Task<TicketViewer> ViewerAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var role = await db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync(cancellationToken);
        var teams = await db.TeamMemberships.Where(m => m.UserId == userId).Select(m => m.TeamId).ToListAsync(cancellationToken);
        return new TicketViewer(userId, role, teams);
    }

    public IQueryable<Ticket> Visible(TicketViewer viewer)
    {
        var teamIds = viewer.TeamIds;
        return viewer.Role switch
        {
            UserRole.Admin => db.Tickets,
            UserRole.ItStaff => db.Tickets.Where(t => t.RequesterId == viewer.UserId || teamIds.Contains(t.TeamId)),
            _ => db.Tickets.Where(t => t.RequesterId == viewer.UserId),
        };
    }

    /// <summary>Loads a tracked ticket in scope by its number, or throws not found.</summary>
    public async Task<(Ticket Ticket, TicketViewer Viewer)> LoadAsync(string number, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var normalized = TicketNumber.Normalize(number);
        var ticket = await Visible(viewer).SingleOrDefaultAsync(t => t.Number == normalized, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Ticket);
        return (ticket, viewer);
    }

    /// <summary>Like <see cref="LoadAsync"/>, but only for viewers who may work on the ticket; others get not found.</summary>
    public async Task<(Ticket Ticket, TicketViewer Viewer)> LoadForWorkAsync(string number, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await LoadAsync(number, cancellationToken);
        return viewer.CanWork(ticket) ? (ticket, viewer) : throw new NotFoundException(AuditEntityTypes.Ticket);
    }
}
