using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests;

public sealed class ApprovalTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Ticket = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static string CodeOf(Action action) => Assert.Throws<DomainException>(action).Code;

    [Fact]
    public void Only_the_assigned_approver_decides_once()
    {
        var approval = Approval.Request(Ticket, Approver, Now);

        Assert.Equal(ErrorCodes.ApprovalNotApprover, CodeOf(() => approval.Approve(Other, Now)));
        approval.Approve(Approver, Now.AddMinutes(5));

        Assert.Equal(ApprovalStatus.Approved, approval.Status);
        Assert.Equal((Approver, Now.AddMinutes(5)), (approval.DecidedBy, approval.DecidedAt));
        Assert.Equal(ErrorCodes.ApprovalNotPending, CodeOf(() => approval.Reject(Approver, "Geç kaldı", Now)));
    }

    [Fact]
    public void Approval_without_approver_waits_for_reassignment()
    {
        var approval = Approval.Request(Ticket, null, Now);

        Assert.Equal(ErrorCodes.ApprovalNotApprover, CodeOf(() => approval.Approve(Other, Now)));
        approval.Reassign(Other);
        approval.Reject(Other, " Bütçe yok ", Now);

        Assert.Equal((ApprovalStatus.Rejected, "Bütçe yok"), (approval.Status, approval.Reason));
    }

    [Fact]
    public void Reject_needs_a_reason()
    {
        var approval = Approval.Request(Ticket, Approver, Now);

        Assert.Equal(ErrorCodes.ApprovalReasonRequired, CodeOf(() => approval.Reject(Approver, " ", Now)));
    }

    [Fact]
    public void Only_an_approved_approval_can_be_voided_and_only_a_pending_one_reassigned()
    {
        var approval = Approval.Request(Ticket, Approver, Now);
        Assert.Equal(ErrorCodes.ApprovalNotPending, CodeOf(() => approval.Void("Kapsam değişti", Now)));

        approval.Approve(Approver, Now);
        approval.Void(" Kapsam değişti ", Now);

        Assert.Equal((ApprovalStatus.Void, "Kapsam değişti"), (approval.Status, approval.Reason));
        Assert.Equal(ErrorCodes.ApprovalNotPending, CodeOf(() => approval.Reassign(Other)));
    }

    [Fact]
    public void Withdraw_closes_only_a_pending_approval()
    {
        var approval = Approval.Request(Ticket, Approver, Now);

        approval.Withdraw(Now);

        Assert.Equal(ApprovalStatus.Void, approval.Status);
        Assert.Equal(ErrorCodes.ApprovalNotPending, CodeOf(() => approval.Withdraw(Now)));
    }
}
