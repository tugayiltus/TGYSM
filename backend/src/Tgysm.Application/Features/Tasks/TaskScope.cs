using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Tasks;

/// <summary>The signed-in user as task access sees them (ADR-0031).</summary>
public sealed record TaskViewer(Guid UserId, UserRole Role, IReadOnlyList<Guid> TeamIds)
{
    public bool IsStaff => Role != UserRole.Employee;

    /// <summary>Same rule as <see cref="TaskScope.Visible"/>, for tasks already loaded.</summary>
    public bool CanSee(WorkTask task) =>
        Role == UserRole.Admin
        || (Role == UserRole.ItStaff && (TeamIds.Contains(task.TeamId) || task.AssigneeId == UserId || task.CreatedById == UserId));

    /// <summary>May work on the task: admin always, IT staff who are members of the task's team.</summary>
    public bool CanWork(WorkTask task) => Role == UserRole.Admin || (Role == UserRole.ItStaff && TeamIds.Contains(task.TeamId));
}

/// <summary>
/// Task access: admins see all; IT staff see tasks of their teams, assigned to them or created by them; employees
/// see none. Everything outside scope is "not found", so numbers do not leak (ADR-0013, ADR-0031).
/// </summary>
public sealed class TaskScope(IAppDbContext db, ICurrentActor actor)
{
    public async Task<TaskViewer> ViewerAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var role = await db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync(cancellationToken);
        var teams = await db.TeamMemberships.Where(m => m.UserId == userId).Select(m => m.TeamId).ToListAsync(cancellationToken);
        return new TaskViewer(userId, role, teams);
    }

    /// <summary>Viewer for creating or listing tasks; employees get not found.</summary>
    public async Task<TaskViewer> StaffViewerAsync(CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return viewer.IsStaff ? viewer : throw new NotFoundException(AuditEntityTypes.Task);
    }

    public IQueryable<WorkTask> Visible(TaskViewer viewer)
    {
        var teamIds = viewer.TeamIds;
        return viewer.Role switch
        {
            UserRole.Admin => db.WorkTasks,
            UserRole.ItStaff => db.WorkTasks.Where(t => teamIds.Contains(t.TeamId) || t.AssigneeId == viewer.UserId || t.CreatedById == viewer.UserId),
            _ => db.WorkTasks.Where(_ => false),
        };
    }

    /// <summary>Loads a tracked task in scope by its number, or throws not found.</summary>
    public async Task<(WorkTask Task, TaskViewer Viewer)> LoadAsync(string number, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var normalized = TaskNumber.Normalize(number);
        var task = await Visible(viewer).SingleOrDefaultAsync(t => t.Number == normalized, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Task);
        return (task, viewer);
    }

    /// <summary>Like <see cref="LoadAsync"/>, but only for viewers who may work on the task; others get not found.</summary>
    public async Task<(WorkTask Task, TaskViewer Viewer)> LoadForWorkAsync(string number, CancellationToken cancellationToken)
    {
        var (task, viewer) = await LoadAsync(number, cancellationToken);
        return viewer.CanWork(task) ? (task, viewer) : throw new NotFoundException(AuditEntityTypes.Task);
    }
}
