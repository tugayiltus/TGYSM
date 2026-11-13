using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Changes;

/// <summary>How much of a change the viewer may see (ADR-0031): everything, the working summary, or nothing (404).</summary>
public enum ChangeAccess
{
    None,
    Summary,
    Full,
}

/// <summary>The signed-in user as change access sees them.</summary>
public sealed record ChangeViewer(Guid UserId, UserRole Role, IReadOnlyList<Guid> TeamIds)
{
    public bool IsStaff => Role != UserRole.Employee;

    public bool IsAdmin => Role == UserRole.Admin;

    /// <summary>May edit and run the change: admin, or a member of the owner team.</summary>
    public bool CanWork(ChangeRequest change) => IsAdmin || (IsStaff && TeamIds.Contains(change.OwnerTeamId));
}

/// <summary>
/// Change access: admins and owner team members (and an admin who decided its approval) see everything; members of a
/// team with a task on the change, and those tasks' assignees, see a working summary; everyone else gets "not found"
/// so numbers do not leak (ADR-0013, ADR-0031).
/// </summary>
public sealed class ChangeScope(IAppDbContext db, ICurrentActor actor)
{
    public async Task<ChangeViewer> ViewerAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var role = await db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync(cancellationToken);
        var teams = await db.TeamMemberships.Where(m => m.UserId == userId).Select(m => m.TeamId).ToListAsync(cancellationToken);
        return new ChangeViewer(userId, role, teams);
    }

    /// <summary>Viewer for creating or listing changes; employees get not found.</summary>
    public async Task<ChangeViewer> StaffViewerAsync(CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return viewer.IsStaff ? viewer : throw new NotFoundException(AuditEntityTypes.Change);
    }

    /// <summary>Changes the viewer may see in full or as a summary; used by lists.</summary>
    public IQueryable<ChangeRequest> Visible(ChangeViewer viewer)
    {
        var teamIds = viewer.TeamIds;
        var userId = viewer.UserId;
        return viewer.Role switch
        {
            UserRole.Admin => db.ChangeRequests,
            UserRole.ItStaff => db.ChangeRequests.Where(c =>
                teamIds.Contains(c.OwnerTeamId)
                || db.WorkTasks.Any(t => t.ChangeId == c.Id && (teamIds.Contains(t.TeamId) || t.AssigneeId == userId))),
            _ => db.ChangeRequests.Where(_ => false),
        };
    }

    public async Task<ChangeAccess> AccessAsync(ChangeRequest change, ChangeViewer viewer, CancellationToken cancellationToken)
    {
        if (!viewer.IsStaff)
        {
            return ChangeAccess.None;
        }

        if (viewer.IsAdmin || viewer.TeamIds.Contains(change.OwnerTeamId)
            || await db.ChangeApprovals.AnyAsync(a => a.ChangeId == change.Id && a.DecidedById == viewer.UserId, cancellationToken))
        {
            return ChangeAccess.Full;
        }

        var teamIds = viewer.TeamIds;
        var hasTask = await db.WorkTasks.AnyAsync(
            t => t.ChangeId == change.Id && (teamIds.Contains(t.TeamId) || t.AssigneeId == viewer.UserId), cancellationToken);
        return hasTask ? ChangeAccess.Summary : ChangeAccess.None;
    }

    /// <summary>Loads a tracked change by number, or throws not found when the viewer has no access.</summary>
    public async Task<(ChangeRequest Change, ChangeViewer Viewer, ChangeAccess Access)> LoadAsync(string number, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var normalized = ChangeNumber.Normalize(number);
        var change = await db.ChangeRequests.SingleOrDefaultAsync(c => c.Number == normalized, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Change);
        var access = await AccessAsync(change, viewer, cancellationToken);
        return access == ChangeAccess.None ? throw new NotFoundException(AuditEntityTypes.Change) : (change, viewer, access);
    }

    /// <summary>Like <see cref="LoadAsync"/>, but only for viewers who may work on the change; others get not found.</summary>
    public async Task<(ChangeRequest Change, ChangeViewer Viewer)> LoadForWorkAsync(string number, CancellationToken cancellationToken)
    {
        var (change, viewer, _) = await LoadAsync(number, cancellationToken);
        return viewer.CanWork(change) ? (change, viewer) : throw new NotFoundException(AuditEntityTypes.Change);
    }
}
