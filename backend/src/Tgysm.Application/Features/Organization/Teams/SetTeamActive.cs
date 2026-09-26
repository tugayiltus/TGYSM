using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record SetTeamActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed class SetTeamActiveHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task<TeamDetail> HandleAsync(SetTeamActiveCommand command, CancellationToken cancellationToken)
    {
        var team = await db.Teams.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Team);
        if (team.IsActive != command.IsActive)
        {
            db.SetExpectedVersion(team, command.Version);
            if (command.IsActive)
            {
                team.Reactivate();
            }
            else
            {
                team.Deactivate();
            }

            audit.Record(command.IsActive ? AuditActions.TeamReactivated : AuditActions.TeamDeactivated, AuditEntityTypes.Team, team.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }
}
