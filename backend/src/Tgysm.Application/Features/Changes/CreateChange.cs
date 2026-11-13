using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Changes;

/// <summary>Plan field limits shared by create and edit.</summary>
internal sealed class ChangeDraftValidator : AbstractValidator<ChangeDraft>
{
    public ChangeDraftValidator()
    {
        RuleFor(d => d.Title).NotEmpty().MaximumLength(ChangeRequest.TitleMaxLength);
        RuleFor(d => d.Justification).MaximumLength(ChangeRequest.TextMaxLength);
        RuleFor(d => d.AffectedService).MaximumLength(ChangeRequest.AffectedServiceMaxLength);
        RuleFor(d => d.Risk).IsInEnum();
        RuleFor(d => d.Impact).IsInEnum();
        RuleFor(d => d.ImplementationPlan).MaximumLength(ChangeRequest.TextMaxLength);
        RuleFor(d => d.VerificationPlan).MaximumLength(ChangeRequest.TextMaxLength);
        RuleFor(d => d.BackoutPlan).MaximumLength(ChangeRequest.TextMaxLength);
    }
}

public sealed record CreateChangeCommand(ChangeDraft Draft, Guid OwnerTeamId, Guid OwnerId, IReadOnlyList<Guid> AssetIds);

internal sealed class CreateChangeValidator : AbstractValidator<CreateChangeCommand>
{
    public CreateChangeValidator()
    {
        RuleFor(c => c.Draft).NotNull();
        RuleFor(c => c.OwnerTeamId).NotEmpty();
        RuleFor(c => c.OwnerId).NotEmpty();
        RuleFor(c => c.AssetIds).NotNull();
    }
}

/// <summary>
/// IT staff open a change for a team they belong to (admins for any team). The owner must be an active IT member of
/// that team. The change starts as a draft (ADR-0032).
/// </summary>
public sealed class CreateChangeHandler(
    IAppDbContext db, IValidator<CreateChangeCommand> validator, IValidator<ChangeDraft> draftValidator, ITicketNumberGenerator numbers,
    ChangeScope scope, ChangeWork work, IAuditLog audit, TimeProvider clock)
{
    public async Task<ChangeDetail> HandleAsync(CreateChangeCommand command, CancellationToken cancellationToken)
    {
        var viewer = await scope.StaffViewerAsync(cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await draftValidator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        if (!viewer.IsAdmin && !viewer.TeamIds.Contains(command.OwnerTeamId))
        {
            throw new NotFoundException(AuditEntityTypes.Team);
        }

        await Features.Tasks.TaskRules.EnsureActiveTeamAsync(db, command.OwnerTeamId, cancellationToken);
        await EnsureOwnerInTeamAsync(db, command.OwnerTeamId, command.OwnerId, cancellationToken);
        var assetIds = await ChangeAssetLinks.ResolveAsync(db, command.AssetIds, cancellationToken);

        ChangeRequest? change = null;
        await db.RunInTransactionAsync(async ct =>
        {
            var now = clock.GetUtcNow();
            var number = ChangeNumber.Format(now.Year, await numbers.NextAsync(ChangeNumber.CounterKind, now.Year, ct));
            change = ChangeRequest.Create(number, command.Draft, command.OwnerTeamId, command.OwnerId, viewer.UserId, now);
            db.ChangeRequests.Add(change);
            await ChangeAssetLinks.SyncAsync(db, change, assetIds, now, ct);
            ChangeHistory.Record(db, change, ChangeEventKind.Created, viewer.UserId, now, new { change.OwnerTeamId, change.OwnerId, AssetIds = assetIds });
            audit.Record(AuditActions.ChangeCreated, AuditEntityTypes.Change, change.Id, newValues: new { change.Number, change.OwnerTeamId, change.OwnerId, AssetIds = assetIds });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await work.DetailAsync(change!.Id, viewer, cancellationToken);
    }

    internal static async Task EnsureOwnerInTeamAsync(IAppDbContext db, Guid teamId, Guid ownerId, CancellationToken cancellationToken)
    {
        var member = await (
            from m in db.TeamMemberships
            join u in db.Users on m.UserId equals u.Id
            where m.TeamId == teamId && u.Id == ownerId && u.IsActive && u.Role != UserRole.Employee
            select m).AnyAsync(cancellationToken);
        if (!member)
        {
            throw new DomainException(ErrorCodes.ChangeOwnerNotInTeam, "The owner is not an active member of the owner team.");
        }
    }
}
