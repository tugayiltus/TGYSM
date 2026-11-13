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

    /// <summary>Everyone who submitted or changed the plan of the pending approval; none of them can decide it.</summary>
    public static IQueryable<Guid> PendingEditorIds(IAppDbContext db, Guid changeId) =>
        from e in db.ChangeApprovalEditors
        join a in db.ChangeApprovals on e.ApprovalId equals a.Id
        where a.ChangeId == changeId && a.Status == ChangeApprovalStatus.Pending
        select e.UserId;

    public static async Task<bool> HasEligibleApproverAsync(IAppDbContext db, ChangeRequest change, CancellationToken cancellationToken)
    {
        var editors = PendingEditorIds(db, change.Id);
        return await db.Users.AnyAsync(
            u => u.IsActive && u.Role == UserRole.Admin && u.Id != change.CreatedById && u.Id != change.OwnerId && !editors.Contains(u.Id),
            cancellationToken);
    }

    private async Task AddEditorAsync(Guid approvalId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await db.ChangeApprovalEditors.AnyAsync(e => e.ApprovalId == approvalId && e.UserId == userId, cancellationToken))
        {
            db.ChangeApprovalEditors.Add(ChangeApprovalEditor.Create(approvalId, userId));
        }
    }

    private void AddPending(ChangeRequest change, string fingerprint, Guid actorUserId, DateTimeOffset now)
    {
        var approval = ChangeApproval.Request(change.Id, fingerprint, now);
        db.ChangeApprovals.Add(approval);
        db.ChangeApprovalEditors.Add(ChangeApprovalEditor.Create(approval.Id, actorUserId));
    }

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
        AddPending(change, await FingerprintAsync(db, change, null, cancellationToken), actorUserId, now);
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
    /// After a plan change (edit, or an Uygulama task added, cancelled or reopened): a submitted change must stay ready;
    /// a pending approval takes the new fingerprint and the actor joins its editors; an approved plan that no
    /// longer matches is voided. The plan tasks are read from the database, so a task change is saved first.
    /// </summary>
    public async Task PlanChangedAsync(
        ChangeRequest change, IReadOnlyList<Guid>? assetIds, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (change.Status is not (ChangeStatus.PendingApproval or ChangeStatus.Scheduled))
        {
            return;
        }

        // A submitted change keeps the rules submit checked: a change cannot strip its window, a plan or its Uygulama task.
        change.RequireReady(await PlanTasks(db, change.Id).CountAsync(cancellationToken));
        var fingerprint = await FingerprintAsync(db, change, assetIds, cancellationToken);
        if (change.Status == ChangeStatus.PendingApproval)
        {
            var pending = await db.ChangeApprovals.SingleAsync(a => a.ChangeId == change.Id && a.Status == ChangeApprovalStatus.Pending, cancellationToken);
            if (pending.Fingerprint != fingerprint)
            {
                pending.UpdateFingerprint(fingerprint);
                await AddEditorAsync(pending.Id, actorUserId, cancellationToken);
            }

            return;
        }

        var approvedFingerprint = await db.ChangeApprovals
            .Where(a => a.ChangeId == change.Id && a.Status == ChangeApprovalStatus.Approved)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => a.Fingerprint).FirstOrDefaultAsync(cancellationToken)
            ?? throw new Domain.Common.DomainException(Domain.Common.ErrorCodes.ChangeApprovalNotPending, "The change has no approved approval.");
        if (approvedFingerprint != fingerprint)
        {
            await VoidAsync(change, PlanChangedReason, actorUserId, now, assetIds, cancellationToken);
        }
    }

    /// <summary>A cancelled change leaves the approval queue: its pending row counts as withdrawn.</summary>
    public async Task CloseOpenRequestAsync(ChangeRequest change, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await db.ChangeApprovals.SingleOrDefaultAsync(a => a.ChangeId == change.Id && a.Status == ChangeApprovalStatus.Pending, cancellationToken);
        pending?.Withdraw(now);
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
        AddPending(change, await FingerprintAsync(db, change, assetIds, cancellationToken), actorUserId, now);
        ChangeHistory.Record(db, change, ChangeEventKind.ApprovalVoided, actorUserId, now, new { Reason = reason });
        audit.Record(AuditActions.ChangeApprovalVoided, AuditEntityTypes.Change, change.Id, newValues: new { change.Number }, reason: reason);
        var data = approverId is { } id ? new Dictionary<string, string> { [OutboxEvent.ApproverIdKey] = id.ToString() } : null;
        outbox.AddForSubject(NotificationEventTypes.ChangeApprovalRequested, OutboxSubjectTypes.Change, change.Id, userId: null, data);
    }
}
