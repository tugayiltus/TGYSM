using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record CreateTeamCommand(string Name);

internal sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamValidator() => RuleFor(c => c.Name).ValidTeamName();
}

public sealed class CreateTeamHandler(IAppDbContext db, IValidator<CreateTeamCommand> validator, IAuditLog audit)
{
    public async Task<TeamDetail> HandleAsync(CreateTeamCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await TeamQueries.EnsureNameFreeAsync(db, command.Name, exceptId: null, cancellationToken);

        var team = Team.Create(command.Name);
        db.Teams.Add(team);
        audit.Record(AuditActions.TeamCreated, AuditEntityTypes.Team, team.Id, newValues: new { team.Name });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.TeamNameTaken, TeamQueries.NameTakenMessage, cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }
}
