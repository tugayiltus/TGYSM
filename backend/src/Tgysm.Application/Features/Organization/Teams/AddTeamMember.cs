using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record AddTeamMemberCommand(Guid TeamId, Guid UserId);

/// <summary>Only active IT staff and admins can join an active team (ADR-0005).</summary>
public sealed class AddTeamMemberHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock)
{
    private const string MembershipExistsMessage = "The user is already a member of this team.";

    public async Task<TeamDetail> HandleAsync(AddTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var team = await db.Teams.AsNoTracking().SingleOrDefaultAsync(t => t.Id == command.TeamId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Team);
        if (!team.IsActive)
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "The team is inactive.");
        }

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        if (user.Role == UserRole.Employee)
        {
            throw new DomainException(ErrorCodes.MemberRoleInvalid, "Only IT staff and admins can be team members.");
        }

        if (!user.IsActive)
        {
            throw new DomainException(ErrorCodes.MemberInactive, "Inactive users cannot join a team.");
        }

        if (await db.TeamMemberships.AnyAsync(m => m.TeamId == team.Id && m.UserId == user.Id, cancellationToken))
        {
            throw new DomainException(ErrorCodes.MembershipExists, MembershipExistsMessage);
        }

        db.TeamMemberships.Add(TeamMembership.Create(team.Id, user.Id, clock.GetUtcNow()));
        audit.Record(AuditActions.TeamMemberAdded, AuditEntityTypes.Team, team.Id, newValues: new { UserId = user.Id });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.MembershipExists, MembershipExistsMessage, cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }
}
