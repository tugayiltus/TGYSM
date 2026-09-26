using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record RemoveTeamMemberCommand(Guid TeamId, Guid UserId);

/// <summary>Removes the membership row; the audit trail keeps the history.</summary>
public sealed class RemoveTeamMemberHandler(IAppDbContext db, IAuditLog audit)
{
    private const string MembershipEntity = "team_membership";

    public async Task<TeamDetail> HandleAsync(RemoveTeamMemberCommand command, CancellationToken cancellationToken)
    {
        var membership = await db.TeamMemberships
            .SingleOrDefaultAsync(m => m.TeamId == command.TeamId && m.UserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException(MembershipEntity);

        db.TeamMemberships.Remove(membership);
        audit.Record(AuditActions.TeamMemberRemoved, AuditEntityTypes.Team, command.TeamId, oldValues: new { command.UserId });
        await db.SaveChangesAsync(cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, command.TeamId, cancellationToken);
    }
}
