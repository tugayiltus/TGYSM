using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Tasks;

/// <summary>Reference checks shared by task creation, assignment and transfer.</summary>
internal static class TaskRules
{
    public static async Task EnsureActiveTeamAsync(IAppDbContext db, Guid teamId, CancellationToken cancellationToken)
    {
        if (!await db.Teams.AnyAsync(t => t.Id == teamId && t.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "The team is inactive or missing.");
        }
    }

    /// <summary>The assignee must be active IT staff or admin and a member of the task's team.</summary>
    public static async Task EnsureAssigneeInTeamAsync(IAppDbContext db, Guid teamId, Guid assigneeId, CancellationToken cancellationToken)
    {
        var member = await (
            from m in db.TeamMemberships
            join u in db.Users on m.UserId equals u.Id
            where m.TeamId == teamId && u.Id == assigneeId && u.IsActive && u.Role != UserRole.Employee
            select m).AnyAsync(cancellationToken);
        if (!member)
        {
            throw new DomainException(ErrorCodes.TaskAssigneeNotInTeam, "The assignee is not an active member of the task's team.");
        }
    }
}
