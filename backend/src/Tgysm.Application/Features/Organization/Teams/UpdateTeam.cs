using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Teams;

/// <summary><paramref name="IsDefaultIntake"/> null keeps the flag; true moves it here from any other team.</summary>
public sealed record UpdateTeamCommand(Guid Id, uint Version, string Name, bool? IsDefaultIntake = null);

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

        var before = new { team.Name, team.IsDefaultIntake };
        team.Rename(command.Name);
        await db.RunSerializedAsync(SerializationLocks.DefaultIntake, async ct =>
        {
            if (command.IsDefaultIntake is { } intake && intake != team.IsDefaultIntake)
            {
                await MoveIntakeAsync(team, intake, ct);
            }

            audit.Record(AuditActions.TeamUpdated, AuditEntityTypes.Team, team.Id, before, new { team.Name, team.IsDefaultIntake });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.TeamNameTaken, TeamQueries.NameTakenMessage, ct);
        }, cancellationToken);
        return await TeamQueries.LoadDetailAsync(db, team.Id, cancellationToken);
    }

    /// <summary>Clears the current intake team first (its own save), so the partial unique index never sees two.</summary>
    private async Task MoveIntakeAsync(Domain.Organization.Team team, bool intake, CancellationToken cancellationToken)
    {
        if (!intake)
        {
            team.ClearDefaultIntake();
            return;
        }

        if (!team.IsActive)
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "An inactive team cannot receive new tickets.");
        }

        var current = await db.Teams.SingleOrDefaultAsync(t => t.IsDefaultIntake && t.Id != team.Id, cancellationToken);
        if (current is not null)
        {
            current.ClearDefaultIntake();
            await db.SaveChangesAsync(cancellationToken);
        }

        team.MarkDefaultIntake();
    }
}
