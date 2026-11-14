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
    /// <summary>The Uygulama tasks the fingerprint covers: open or completed, never cancelled.</summary>
    public static IQueryable<WorkTask> PlanTasks(IAppDbContext db, Guid changeId) =>
        db.WorkTasks.Where(t => t.ChangeId == changeId && t.Phase == ChangePhase.Implementation && t.Status != WorkTaskStatus.Cancelled);

    /// <summary>
    /// Every (change, admin) pair where the admin may decide the change's approval: an active admin who is not the
    /// author, not the owner and never a plan editor of the change. The one rule for deciding, listing and mailing.
    /// </summary>
    public static IQueryable<ChangeDecider> Deciders(IAppDbContext db) =>
        from c in db.ChangeRequests
        from u in db.Users
        where u.IsActive && u.Role == UserRole.Admin && u.Id != c.CreatedById && u.Id != c.OwnerId
            && !db.ChangePlanEditors.Any(e => e.ChangeId == c.Id && e.UserId == u.Id)
        select new ChangeDecider { ChangeId = c.Id, UserId = u.Id };

    public static Task<bool> HasEligibleApproverAsync(IAppDbContext db, ChangeRequest change, CancellationToken cancellationToken) =>
        Deciders(db).AnyAsync(d => d.ChangeId == change.Id, cancellationToken);

    public static Task<bool> CanDecideAsync(IAppDbContext db, Guid changeId, Guid userId, CancellationToken cancellationToken) =>
        Deciders(db).AnyAsync(d => d.ChangeId == changeId && d.UserId == userId, cancellationToken);

    /// <summary>
    /// Records <paramref name="userId"/> as a plan editor of the change for its whole life: the submitter, and whoever
    /// changes a fingerprint field, the asset set or the Uygulama tasks, in any status.
    /// </summary>
    public async Task RecordEditorAsync(Guid changeId, Guid userId, CancellationToken cancellationToken)
    {
        if (db.ChangePlanEditors.Local.Any(e => e.ChangeId == changeId && e.UserId == userId)
            || await db.ChangePlanEditors.AnyAsync(e => e.ChangeId == changeId && e.UserId == userId, cancellationToken))
        {
            return;
        }

        db.ChangePlanEditors.Add(ChangePlanEditor.Create(changeId, userId));
    }

    private void AddPending(ChangeRequest change, string fingerprint, DateTimeOffset now) =>
        db.ChangeApprovals.Add(ChangeApproval.Request(change.Id, fingerprint, now));

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
        AddPending(change, await FingerprintAsync(db, change, null, cancellationToken), now);
        await RecordEditorAsync(change.Id, actorUserId, cancellationToken);
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
    /// a pending approval takes the new fingerprint; an approved plan that no longer matches is voided. The caller
    /// records the actor with <see cref="RecordEditorAsync"/>. The plan tasks are read from the database, so a task change is saved first.
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
            await VoidAsync(change, ChangeReasonCodes.PlanChanged, actorUserId, now, assetIds, cancellationToken);
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
        AddPending(change, await FingerprintAsync(db, change, assetIds, cancellationToken), now);
        ChangeHistory.Record(db, change, ChangeEventKind.ApprovalVoided, actorUserId, now, new { Reason = reason });
        audit.Record(AuditActions.ChangeApprovalVoided, AuditEntityTypes.Change, change.Id, newValues: new { change.Number }, reason: reason);
        var data = approverId is { } id ? new Dictionary<string, string> { [OutboxEvent.ApproverIdKey] = id.ToString() } : null;
        outbox.AddForSubject(NotificationEventTypes.ChangeApprovalRequested, OutboxSubjectTypes.Change, change.Id, userId: null, data);
    }
}

/// <summary>One change and one admin who may decide its approval (<see cref="ChangeApprovalFlow.Deciders"/>).</summary>
public sealed class ChangeDecider
{
    public Guid ChangeId { get; init; }
    public Guid UserId { get; init; }
}
