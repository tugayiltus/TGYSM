using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Changes;

/// <summary>
/// Approval life of a change (ADR-0032): request on submit, take back on withdraw, void when an approved plan changes.
/// The decision itself is <see cref="DecideChangeApprovalHandler"/>. Callers run inside the change lock and save once.
/// </summary>
public sealed class ChangeApprovalFlow(IAppDbContext db, INotificationOutbox outbox, IAuditLog audit)
{
    public const string PlanChangedReason = "Plan değişti";

    /// <summary>The Uygulama tasks the fingerprint covers: open or completed, never cancelled.</summary>
    public static IQueryable<WorkTask> PlanTasks(IAppDbContext db, Guid changeId) =>
        db.WorkTasks.Where(t => t.ChangeId == changeId && t.Phase == ChangePhase.Implementation && t.Status != WorkTaskStatus.Cancelled);

    /// <summary>The author and the owner never decide; the caller checks the admin role.</summary>
    public static bool IsEligible(Guid userId, ChangeRequest change) => userId != change.CreatedById && userId != change.OwnerId;

    public static Task<bool> HasEligibleApproverAsync(IAppDbContext db, ChangeRequest change, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(u => u.IsActive && u.Role == UserRole.Admin && u.Id != change.CreatedById && u.Id != change.OwnerId, cancellationToken);

    /// <summary>Fingerprint of the saved plan; pass <paramref name="assetIds"/> when the asset links are not saved yet.</summary>
    public static async Task<string> FingerprintAsync(
        IAppDbContext db, ChangeRequest change, IReadOnlyList<Guid>? assetIds, CancellationToken cancellationToken)
    {
        assetIds ??= await db.ChangeAssets.Where(l => l.ChangeId == change.Id).Select(l => l.AssetId).ToListAsync(cancellationToken);
        var taskIds = await PlanTasks(db, change.Id).Select(t => t.Id).ToListAsync(cancellationToken);
        return ChangePlanFingerprint.Compute(change, assetIds, taskIds);
    }

    /// <summary>Draft → pending approval with a pending row for the current plan.</summary>
    public async Task RequestAsync(ChangeRequest change, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var taskCount = await PlanTasks(db, change.Id).CountAsync(cancellationToken);
        change.Submit(taskCount, now);
        db.ChangeApprovals.Add(ChangeApproval.Request(change.Id, await FingerprintAsync(db, change, null, cancellationToken), now));
        ChangeHistory.Record(db, change, ChangeEventKind.Submitted, actorUserId, now);
        audit.Record(AuditActions.ChangeStatusChanged, AuditEntityTypes.Change, change.Id, newValues: new { change.Number, change.Status });
        outbox.AddForSubject(NotificationEventTypes.ChangeApprovalRequested, OutboxSubjectTypes.Change, change.Id, userId: null);
    }

    /// <summary>Pending approval → draft: the author takes the request back.</summary>
    public async Task WithdrawAsync(ChangeRequest change, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await db.ChangeApprovals.SingleOrDefaultAsync(a => a.ChangeId == change.Id && a.Status == ChangeApprovalStatus.Pending, cancellationToken)
            ?? throw new Domain.Common.DomainException(Domain.Common.ErrorCodes.ChangeApprovalNotPending, "The change has no pending approval.");
        change.ReturnToDraft(now);
        pending.Withdraw(now);
        ChangeHistory.Record(db, change, ChangeEventKind.Withdrawn, actorUserId, now);
        audit.Record(AuditActions.ChangeStatusChanged, AuditEntityTypes.Change, change.Id, newValues: new { change.Number, change.Status });
    }

    /// <summary>
    /// A fingerprint field changed after approval: the approved row becomes void, a new pending row asks again and the
    /// admin who approved is told. <paramref name="assetIds"/> is the new asset set when it is not saved yet.
    /// </summary>
    public async Task VoidAsync(
        ChangeRequest change, string reason, Guid actorUserId, DateTimeOffset now, IReadOnlyList<Guid>? assetIds, CancellationToken cancellationToken)
    {
        var approved = await db.ChangeApprovals
            .Where(a => a.ChangeId == change.Id && a.Status == ChangeApprovalStatus.Approved)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Domain.Common.DomainException(Domain.Common.ErrorCodes.ChangeApprovalNotPending, "The change has no approved approval.");
        var approverId = approved.DecidedById;
        approved.Void(reason, now);
        change.BackToPending(now);
        db.ChangeApprovals.Add(ChangeApproval.Request(change.Id, await FingerprintAsync(db, change, assetIds, cancellationToken), now));
        ChangeHistory.Record(db, change, ChangeEventKind.ApprovalVoided, actorUserId, now, new { Reason = reason });
        audit.Record(AuditActions.ChangeApprovalVoided, AuditEntityTypes.Change, change.Id, newValues: new { change.Number }, reason: reason);
        var data = approverId is { } id ? new Dictionary<string, string> { [OutboxEvent.ApproverIdKey] = id.ToString() } : null;
        outbox.AddForSubject(NotificationEventTypes.ChangeApprovalRequested, OutboxSubjectTypes.Change, change.Id, userId: null, data);
    }
}
