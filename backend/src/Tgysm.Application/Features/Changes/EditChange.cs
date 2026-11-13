using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;

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
/// Replaces the plan and the affected assets. While the approval is pending an edit refreshes its fingerprint; an
/// approved (scheduled) change whose fingerprint changes goes back to approval; from implementation on the domain
/// refuses (ADR-0032).
/// </summary>
public sealed class EditChangeHandler(
    IAppDbContext db, IValidator<EditChangeCommand> validator, IValidator<ChangeDraft> draftValidator, ChangeWork work, ChangeApprovalFlow flow, IAuditLog audit)
{
    public async Task<ChangeDetail> HandleAsync(EditChangeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await draftValidator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, async (change, viewer, now, ct) =>
        {
            var assetIds = await ChangeAssetLinks.ResolveAsync(db, command.AssetIds, ct);
            var before = ChangeDraftFields.Of(change);
            change.Edit(command.Draft, now);
            var changed = ChangeDraftFields.Differences(before, ChangeDraftFields.Of(change));
            var (added, removed) = await ChangeAssetLinks.SyncAsync(db, change, assetIds, now, ct);
            if (changed.Count > 0)
            {
                ChangeHistory.Record(db, change, ChangeEventKind.Updated, viewer.UserId, now, new { Fields = changed });
            }

            if (added.Count > 0 || removed.Count > 0)
            {
                ChangeHistory.Record(db, change, ChangeEventKind.AssetsChanged, viewer.UserId, now, new { Added = added, Removed = removed });
            }

            await ApplyApprovalAsync(change, assetIds, viewer.UserId, now, ct);
            audit.Record(AuditActions.ChangeUpdated, AuditEntityTypes.Change, change.Id, newValues: new { change.Number, Fields = changed, Added = added, Removed = removed });
        }, cancellationToken);
    }

    private async Task ApplyApprovalAsync(ChangeRequest change, IReadOnlyList<Guid> assetIds, Guid actorUserId, DateTimeOffset now, CancellationToken ct)
    {
        if (change.Status is not (ChangeStatus.PendingApproval or ChangeStatus.Scheduled))
        {
            return;
        }

        // A submitted change keeps the rules submit checked: an edit cannot strip its window, a plan or its Uygulama task.
        change.RequireReady(await ChangeApprovalFlow.PlanTasks(db, change.Id).CountAsync(ct));
        var fingerprint = await ChangeApprovalFlow.FingerprintAsync(db, change, assetIds, ct);
        if (change.Status == ChangeStatus.PendingApproval)
        {
            var pending = await db.ChangeApprovals.SingleAsync(a => a.ChangeId == change.Id && a.Status == ChangeApprovalStatus.Pending, ct);
            if (pending.Fingerprint != fingerprint)
            {
                pending.UpdateFingerprint(fingerprint, actorUserId);
            }

            return;
        }

        var approvedFingerprint = await db.ChangeApprovals
            .Where(a => a.ChangeId == change.Id && a.Status == ChangeApprovalStatus.Approved)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => a.Fingerprint).FirstOrDefaultAsync(ct)
            ?? throw new Domain.Common.DomainException(Domain.Common.ErrorCodes.ChangeApprovalNotPending, "The change has no approved approval.");
        if (approvedFingerprint != fingerprint)
        {
            await flow.VoidAsync(change, ChangeApprovalFlow.PlanChangedReason, actorUserId, now, assetIds, ct);
        }
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
