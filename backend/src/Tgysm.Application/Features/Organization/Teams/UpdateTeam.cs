using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record UpdateTeamCommand(Guid Id, uint Version, string Name);

internal sealed class UpdateTeamValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamValidator() => RuleFor(c => c.Name).ValidTeamName();
}

public sealed class UpdateTeamHandler(IAppDbContext db, IValidator<UpdateTeamCommand> validator, IAuditLog audit)
{
    public async Task<TeamDetail> HandleAsync(UpdateTeamCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var team = await db.Teams.SingleOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Team);
        db.SetExpectedVersion(team, command.Version);

        if (TurkishText.ToSearchKey(command.Name) != team.NormalizedName)
        {
            await TeamQueries.EnsureNameFreeAsync(db, command.Name, team.Id, cancellationToken);
        }

        var before = new { team.Name };
        team.Rename(command.Name);
        audit.Record(AuditActions.TeamUpdated, AuditEntityTypes.Team, team.Id, before, new { team.Name });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.TeamNameTaken, TeamQueries.NameTakenMessage, cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }
}
