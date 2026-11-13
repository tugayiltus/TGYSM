using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Changes;

public sealed record EditChangeCommand(string Number, uint Version, ChangeDraft Draft, IReadOnlyList<Guid> AssetIds);

internal sealed class EditChangeValidator : AbstractValidator<EditChangeCommand>
{
    public EditChangeValidator()
    {
        RuleFor(c => c.Number).NotEmpty();
        RuleFor(c => c.Draft).NotNull();
        RuleFor(c => c.AssetIds).NotNull();
    }
}

/// <summary>
/// Replaces the plan and the affected assets of a draft. Other states refuse here until the approval flow decides how
/// an edit there is treated (ADR-0032).
/// </summary>
public sealed class EditChangeHandler(
    IAppDbContext db, IValidator<EditChangeCommand> validator, IValidator<ChangeDraft> draftValidator, ChangeWork work, IAuditLog audit)
{
    public async Task<ChangeDetail> HandleAsync(EditChangeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await draftValidator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        var assetIds = await ChangeAssetLinks.ResolveAsync(db, command.AssetIds, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, async (change, viewer, now) =>
        {
            if (change.Status != ChangeStatus.Draft)
            {
                throw new DomainException(ErrorCodes.ChangeInvalidTransition, "Only a draft change can be edited.");
            }

            var before = ChangeDraftFields.Of(change);
            change.Edit(command.Draft, now);
            var changed = ChangeDraftFields.Differences(before, ChangeDraftFields.Of(change));
            var (added, removed) = await ChangeAssetLinks.SyncAsync(db, change, assetIds, now, CancellationToken.None);
            if (changed.Count > 0)
            {
                ChangeHistory.Record(db, change, ChangeEventKind.Updated, viewer.UserId, now, new { Fields = changed });
            }

            if (added.Count > 0 || removed.Count > 0)
            {
                ChangeHistory.Record(db, change, ChangeEventKind.AssetsChanged, viewer.UserId, now, new { Added = added, Removed = removed });
            }

            audit.Record(AuditActions.ChangeUpdated, AuditEntityTypes.Change, change.Id, newValues: new { change.Number, Fields = changed, Added = added, Removed = removed });
        }, cancellationToken);
    }
}

/// <summary>The editable fields of a change as a name → value map, to name what an edit changed.</summary>
internal static class ChangeDraftFields
{
    public static IReadOnlyDictionary<string, object?> Of(ChangeRequest change) => new Dictionary<string, object?>
    {
        [nameof(ChangeDraft.Title)] = change.Title,
        [nameof(ChangeDraft.Justification)] = change.Justification,
        [nameof(ChangeDraft.AffectedService)] = change.AffectedService,
        [nameof(ChangeDraft.Risk)] = change.Risk,
        [nameof(ChangeDraft.Impact)] = change.Impact,
        [nameof(ChangeDraft.WindowStart)] = change.WindowStart,
        [nameof(ChangeDraft.WindowEnd)] = change.WindowEnd,
        [nameof(ChangeDraft.ImplementationPlan)] = change.ImplementationPlan,
        [nameof(ChangeDraft.VerificationPlan)] = change.VerificationPlan,
        [nameof(ChangeDraft.BackoutPlan)] = change.BackoutPlan,
    };

    public static IReadOnlyList<string> Differences(IReadOnlyDictionary<string, object?> before, IReadOnlyDictionary<string, object?> after) =>
        [.. before.Keys.Where(k => !Equals(before[k], after[k]))];
}
