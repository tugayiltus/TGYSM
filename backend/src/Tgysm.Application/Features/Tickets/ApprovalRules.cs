using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Tickets;

internal static class ApprovalRules
{
    /// <summary>A device can be handed over against a request only once any required approval is granted (spec §3.1).</summary>
    public static async Task EnsureHandoverAllowedAsync(IAppDbContext db, Guid requestTicketId, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == requestTicketId && t.Kind == Domain.Tickets.TicketKind.Request, cancellationToken)
            ?? throw new Common.NotFoundException(Domain.Audit.AuditEntityTypes.Ticket);
        var latest = await TicketQueries.LatestApprovalAsync(db, ticket.Id, cancellationToken);
        if (ticket.RequiresApproval && latest?.Status != Domain.Tickets.ApprovalStatus.Approved)
        {
            throw new Domain.Common.DomainException(Domain.Common.ErrorCodes.TicketApprovalRequired, "The request is not approved.");
        }
    }

    /// <summary>An approver must be an active admin other than the requester.</summary>
    public static Task<bool> IsValidApproverAsync(IAppDbContext db, Guid approverId, Guid requesterId, CancellationToken cancellationToken) =>
        approverId == requesterId
            ? Task.FromResult(false)
            : db.Users.AnyAsync(u => u.Id == approverId && u.IsActive && u.Role == UserRole.Admin, cancellationToken);

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
