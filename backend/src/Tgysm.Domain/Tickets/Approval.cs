using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tickets;

/// <summary>Manager approval of a request (spec §3.4). At most one pending approval per ticket (partial unique index).</summary>
public sealed class Approval : Entity
{
    public const int ReasonMaxLength = 500;

    private Approval()
    {
    }

    public Guid TicketId { get; private set; }

    /// <summary>Null when the requester has no approver or is the approver; an admin reassigns it.</summary>
    public Guid? ApproverId { get; private set; }

    public ApprovalStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }

    public static Approval Request(Guid ticketId, Guid? approverId, DateTimeOffset now) =>
        new() { TicketId = ticketId, ApproverId = approverId, Status = ApprovalStatus.Pending, CreatedAt = now };

    public void Approve(Guid actorUserId, DateTimeOffset now) => Decide(ApprovalStatus.Approved, actorUserId, null, now);

    public void Reject(Guid actorUserId, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(ErrorCodes.ApprovalReasonRequired, "A reason is required.");
        }

        Decide(ApprovalStatus.Rejected, actorUserId, reason.Trim(), now);
    }

    public void Reassign(Guid approverId)
    {
        RequireStatus(ApprovalStatus.Pending);
        ApproverId = approverId;
    }

    /// <summary>Invalidates an approved approval after a scope change.</summary>
    public void Void(string reason, DateTimeOffset now)
    {
        RequireStatus(ApprovalStatus.Approved);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(ErrorCodes.ApprovalReasonRequired, "A reason is required.");
        }

        Status = ApprovalStatus.Void;
        Reason = reason.Trim();
        DecidedAt = now;
    }

    /// <summary>The ticket was cancelled while waiting: the pending approval is closed as void.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        RequireStatus(ApprovalStatus.Pending);
        Status = ApprovalStatus.Void;
        DecidedAt = now;
    }

    private void Decide(ApprovalStatus status, Guid actorUserId, string? reason, DateTimeOffset now)
    {
        RequireStatus(ApprovalStatus.Pending);
        if (ApproverId != actorUserId)
        {
            throw new DomainException(ErrorCodes.ApprovalNotApprover, "Only the assigned approver decides.");
        }

        Status = status;
        Reason = reason;
        DecidedAt = now;
        DecidedBy = actorUserId;
    }

    private void RequireStatus(ApprovalStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainException(ErrorCodes.ApprovalNotPending, $"The approval is {Status}.");
        }
    }
}
