using Tgysm.Domain.Common;

namespace Tgysm.Domain.Changes;

/// <summary>
/// Single-stage approval of a change (ADR-0032). Unassigned: any eligible admin decides. At most one pending row per
/// change (partial unique index). <see cref="Fingerprint"/> is the plan the decision applies to.
/// </summary>
public sealed class ChangeApproval : Entity
{
    public const int ReasonMaxLength = 500;
    public const int FingerprintLength = 64;

    private ChangeApproval()
    {
    }

    public Guid ChangeId { get; private init; }
    public string Fingerprint { get; private set; } = string.Empty;
    public ChangeApprovalStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedById { get; private set; }

    /// <summary>
    /// Who submitted or last changed the plan this row covers. That person cannot decide it, so nobody approves their
    /// own edit (separation of duties).
    /// </summary>
    public Guid? PlanEditorId { get; private set; }

    public static ChangeApproval Request(Guid changeId, string fingerprint, Guid planEditorId, DateTimeOffset now) =>
        new() { ChangeId = changeId, Fingerprint = fingerprint, PlanEditorId = planEditorId, Status = ChangeApprovalStatus.Pending, CreatedAt = now };

    public void Approve(Guid actorUserId, DateTimeOffset now) => Decide(ChangeApprovalStatus.Approved, actorUserId, null, now);

    public void Reject(Guid actorUserId, string reason, DateTimeOffset now) =>
        Decide(ChangeApprovalStatus.Rejected, actorUserId, RequireReason(reason), now);

    /// <summary>"Düzeltmeye gönder": the author revises the plan and submits again.</summary>
    public void Return(Guid actorUserId, string reason, DateTimeOffset now) =>
        Decide(ChangeApprovalStatus.Returned, actorUserId, RequireReason(reason), now);

    /// <summary>The author takes the pending request back.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        RequireStatus(ChangeApprovalStatus.Pending);
        Status = ChangeApprovalStatus.Withdrawn;
        DecidedAt = now;
    }

    /// <summary>An approved approval stops applying because a fingerprint field changed.</summary>
    public void Void(string reason, DateTimeOffset now)
    {
        RequireStatus(ChangeApprovalStatus.Approved);
        Reason = RequireReason(reason);
        Status = ChangeApprovalStatus.Void;
        DecidedAt = now;
    }

    /// <summary>The author edited the plan while the request waits: the decision applies to the new plan.</summary>
    public void UpdateFingerprint(string fingerprint, Guid planEditorId)
    {
        RequireStatus(ChangeApprovalStatus.Pending);
        Fingerprint = fingerprint;
        PlanEditorId = planEditorId;
    }

    private void Decide(ChangeApprovalStatus status, Guid actorUserId, string? reason, DateTimeOffset now)
    {
        RequireStatus(ChangeApprovalStatus.Pending);
        Status = status;
        Reason = reason;
        DecidedAt = now;
        DecidedById = actorUserId;
    }

    private static string RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(ErrorCodes.ApprovalReasonRequired, "A reason is required.");
        }

        return reason.Trim();
    }

    private void RequireStatus(ChangeApprovalStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainException(ErrorCodes.ChangeApprovalNotPending, $"The approval is {Status}.");
        }
    }
}
