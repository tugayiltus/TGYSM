using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tests.Changes;

public sealed class ChangeApprovalTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Admin = Guid.NewGuid();

    private static ChangeApproval Pending() => ChangeApproval.Request(Guid.NewGuid(), "abc", Now);

    private static void AssertCode(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<DomainException>(action).Code);

    [Fact]
    public void Request_starts_pending_with_the_fingerprint()
    {
        var approval = Pending();

        Assert.Equal(ChangeApprovalStatus.Pending, approval.Status);
        Assert.Equal("abc", approval.Fingerprint);
    }

    [Fact]
    public void Approve_records_the_decider()
    {
        var approval = Pending();
        approval.Approve(Admin, Now);

        Assert.Equal((ChangeApprovalStatus.Approved, Admin, Now), (approval.Status, approval.DecidedById, approval.DecidedAt));
    }

    [Fact]
    public void Reject_and_return_need_a_reason_and_keep_it_trimmed()
    {
        AssertCode(ErrorCodes.ApprovalReasonRequired, () => Pending().Reject(Admin, " ", Now));
        AssertCode(ErrorCodes.ApprovalReasonRequired, () => Pending().Return(Admin, "", Now));

        var rejected = Pending();
        rejected.Reject(Admin, " Riskli ", Now);
        var returned = Pending();
        returned.Return(Admin, " Plan eksik ", Now);

        Assert.Equal((ChangeApprovalStatus.Rejected, "Riskli"), (rejected.Status, rejected.Reason));
        Assert.Equal((ChangeApprovalStatus.Returned, "Plan eksik"), (returned.Status, returned.Reason));
    }

    [Fact]
    public void Withdraw_and_fingerprint_update_work_only_while_pending()
    {
        var approval = Pending();
        approval.UpdateFingerprint("def");
        Assert.Equal("def", approval.Fingerprint);
        approval.Withdraw(Now);
        Assert.Equal(ChangeApprovalStatus.Withdrawn, approval.Status);

        AssertCode(ErrorCodes.ChangeApprovalNotPending, () => approval.Withdraw(Now));
        AssertCode(ErrorCodes.ChangeApprovalNotPending, () => approval.UpdateFingerprint("x"));
        AssertCode(ErrorCodes.ChangeApprovalNotPending, () => approval.Approve(Admin, Now));
    }

    [Fact]
    public void Void_applies_only_to_an_approved_approval_and_needs_a_reason()
    {
        AssertCode(ErrorCodes.ChangeApprovalNotPending, () => Pending().Void("Plan değişti", Now));

        var approval = Pending();
        approval.Approve(Admin, Now);
        AssertCode(ErrorCodes.ApprovalReasonRequired, () => approval.Void(" ", Now));
        approval.Void("Plan değişti", Now);

        Assert.Equal((ChangeApprovalStatus.Void, "Plan değişti"), (approval.Status, approval.Reason));
        AssertCode(ErrorCodes.ChangeApprovalNotPending, () => approval.Void("tekrar", Now));
    }
}
