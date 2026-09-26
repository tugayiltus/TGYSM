using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Tickets;

internal static class ApprovalRules
{
    /// <summary>
    /// The requester's department approver if that is an active admin other than the requester; otherwise null and an
    /// admin reassigns it (nobody approves their own request).
    /// </summary>
    public static async Task<Guid?> ApproverForAsync(IAppDbContext db, Guid requesterId, CancellationToken cancellationToken)
    {
        var approver = await (
            from user in db.Users
            join department in db.Departments on user.DepartmentId equals department.Id
            where user.Id == requesterId
            select department.ApproverUserId).SingleOrDefaultAsync(cancellationToken);

        return approver is { } id && id != requesterId
               && await db.Users.AnyAsync(u => u.Id == id && u.IsActive && u.Role == UserRole.Admin, cancellationToken)
            ? id
            : null;
    }
}
