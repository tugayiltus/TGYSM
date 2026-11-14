using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests.Changes;

public sealed class ChangeRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly ChangePhaseTally None = new(0, 0, 0);
    private static readonly ChangeClosing Done = new("Doğrulandı", null, null, null);

    private static ChangeDraft Draft(string title = " Sunucu güncelle ", DateTimeOffset? end = null, string? impl = "Uygula") =>
        new(title, " Gerekçe ", null, Level.Medium, Level.Low, Now.AddDays(1), end ?? Now.AddDays(1).AddHours(2), impl, "Doğrula", "Geri al");

    private static ChangeRequest NewChange() =>
        ChangeRequest.Create("CHG-2026-000001", Draft(), Guid.NewGuid(), Owner, Author, Now);

    private static ChangeRequest InStatus(ChangeStatus target)
    {
        var change = NewChange();
        if (target == ChangeStatus.Draft)
        {
            return change;
        }

        change.Submit(1, Now);
        if (target == ChangeStatus.PendingApproval)
        {
            return change;
        }

        change.Schedule(Now);
        if (target == ChangeStatus.Scheduled)
        {
            return change;
        }

        change.StartImplementation(Now.AddDays(1));
        if (target == ChangeStatus.Implementing)
        {
            return change;
        }

        change.FinishImplementation(Now.AddDays(1));
        return change;
    }

    private static ChangeTaskTally Tally(ChangePhaseTally? prep = null, ChangePhaseTally? impl = null, ChangePhaseTally? verify = null, ChangePhaseTally? backout = null) =>
        new(prep ?? None, impl ?? new(0, 1, 0), verify ?? None, backout ?? None);

    private static void AssertCode(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<DomainException>(action).Code);

    private static void AssertInvalid(Action action) => AssertCode(ErrorCodes.ChangeInvalidTransition, action);

    [Fact]
    public void New_change_is_a_trimmed_draft()
    {
        var change = NewChange();

        Assert.Equal(ChangeStatus.Draft, change.Status);
        Assert.Equal("Sunucu güncelle", change.Title);
        Assert.Equal("Gerekçe", change.Justification);
        Assert.Equal((Owner, Author), (change.OwnerId, change.CreatedById));
        Assert.True(change.IsOpen);
        Assert.False(change.IsPlanLocked);
    }

    [Fact]
    public void Normal_path_runs_to_closed()
    {
        var change = InStatus(ChangeStatus.Verifying);
        change.Close(ChangeOutcome.Successful, Done, Tally(), Now.AddDays(2));

        Assert.Equal((ChangeStatus.Closed, ChangeOutcome.Successful), (change.Status, change.Outcome));
        Assert.Equal(Now.AddDays(1), change.ActualStart);
        Assert.Equal(Now.AddDays(2), change.ActualEnd);
        Assert.Equal("Doğrulandı", change.VerificationNote);
        Assert.False(change.IsOpen);
    }

    [Fact]
    public void Approval_loop_returns_to_draft_and_back_to_pending()
    {
        var change = InStatus(ChangeStatus.PendingApproval);
        change.ReturnToDraft(Now);
        Assert.Equal(ChangeStatus.Draft, change.Status);

        change = InStatus(ChangeStatus.Scheduled);
        change.BackToPending(Now);
        Assert.Equal(ChangeStatus.PendingApproval, change.Status);
    }

    [Fact]
    public void Refused_transitions()
    {
        AssertInvalid(() => InStatus(ChangeStatus.Draft).Schedule(Now));
        AssertInvalid(() => InStatus(ChangeStatus.Draft).ReturnToDraft(Now));
        AssertInvalid(() => InStatus(ChangeStatus.Draft).StartImplementation(Now));
        AssertInvalid(() => InStatus(ChangeStatus.PendingApproval).Submit(1, Now));
        AssertInvalid(() => InStatus(ChangeStatus.PendingApproval).StartImplementation(Now));
        AssertInvalid(() => InStatus(ChangeStatus.PendingApproval).BackToPending(Now));
        AssertInvalid(() => InStatus(ChangeStatus.Scheduled).FinishImplementation(Now));
        AssertInvalid(() => InStatus(ChangeStatus.Scheduled).StartBackout(Now));
        AssertInvalid(() => InStatus(ChangeStatus.Implementing).Cancel("x", Now));
        AssertInvalid(() => InStatus(ChangeStatus.Implementing).StartImplementation(Now));
        AssertInvalid(() => InStatus(ChangeStatus.Verifying).FinishImplementation(Now));
        AssertInvalid(() => InStatus(ChangeStatus.Implementing).Close(ChangeOutcome.Successful, Done, Tally(), Now));
    }

    [Fact]
    public void Submit_needs_window_plans_and_an_implementation_task()
    {
        var noTask = NewChange();
        AssertCode(ErrorCodes.ChangeNotReady, () => noTask.Submit(0, Now));

        var noWindow = ChangeRequest.Create("CHG-2026-000002", Draft() with { WindowStart = null }, Guid.NewGuid(), Owner, Author, Now);
        AssertCode(ErrorCodes.ChangeNotReady, () => noWindow.Submit(1, Now));

        var noPlan = ChangeRequest.Create("CHG-2026-000003", Draft(impl: " "), Guid.NewGuid(), Owner, Author, Now);
        AssertCode(ErrorCodes.ChangeNotReady, () => noPlan.Submit(1, Now));

        var backwards = ChangeRequest.Create("CHG-2026-000004", Draft(end: Now), Guid.NewGuid(), Owner, Author, Now);
        AssertCode(ErrorCodes.ChangeNotReady, () => backwards.Submit(1, Now));
        Assert.Equal(ChangeStatus.Draft, backwards.Status);
    }

    [Fact]
    public void Edit_reports_whether_a_fingerprint_field_changed()
    {
        var change = NewChange();

        Assert.False(change.Edit(Draft(title: "Yeni başlık") with { Justification = "Başka" }, Now));
        Assert.Equal("Yeni başlık", change.Title);
        Assert.True(change.Edit(Draft(end: Now.AddDays(3)), Now));
        Assert.True(change.Edit(Draft(impl: "Başka plan"), Now));
        Assert.True(change.Edit(Draft(impl: "Başka plan") with { Risk = Level.High }, Now));
        Assert.True(change.Edit(Draft(impl: "Başka plan") with { Impact = Level.High, Risk = Level.High }, Now));
    }

    [Fact]
    public void Edit_is_refused_when_locked_or_closed()
    {
        AssertCode(ErrorCodes.ChangePlanLocked, () => InStatus(ChangeStatus.Implementing).Edit(Draft(), Now));
        AssertCode(ErrorCodes.ChangePlanLocked, () => InStatus(ChangeStatus.Verifying).Edit(Draft(), Now));
        Assert.True(InStatus(ChangeStatus.Implementing).IsPlanLocked);

        var cancelled = NewChange();
        cancelled.Cancel("Vazgeçildi", Now);
        AssertInvalid(() => cancelled.Edit(Draft(), Now));
    }

    [Fact]
    public void Edit_is_allowed_while_pending_or_scheduled()
    {
        Assert.True(InStatus(ChangeStatus.PendingApproval).Edit(Draft(impl: "Başka"), Now));
        Assert.True(InStatus(ChangeStatus.Scheduled).Edit(Draft(impl: "Başka"), Now));
    }

    [Fact]
    public void Close_successful_needs_closed_tasks_and_a_note()
    {
        var change = InStatus(ChangeStatus.Verifying);

        AssertCode(ErrorCodes.ChangeOpenTasks, () => change.Close(ChangeOutcome.Successful, Done, Tally(impl: new(1, 0, 0)), Now));
        AssertCode(ErrorCodes.ChangeOpenTasks, () => change.Close(ChangeOutcome.Successful, Done, Tally(verify: new(1, 0, 0)), Now));
        AssertCode(ErrorCodes.ChangeOpenTasks, () => change.Close(ChangeOutcome.Successful, Done, Tally(prep: new(1, 0, 0)), Now));
        AssertCode(ErrorCodes.ChangeVerificationRequired, () => change.Close(ChangeOutcome.Successful, Done with { VerificationNote = " " }, Tally(), Now));
        Assert.Equal(ChangeStatus.Verifying, change.Status);

        change.Close(ChangeOutcome.Successful, Done, Tally(prep: new(0, 1, 1), backout: new(1, 0, 0)), Now.AddDays(2));
        Assert.Equal(ChangeStatus.Closed, change.Status);
    }

    [Fact]
    public void Close_successful_ignores_cancelled_implementation_tasks_but_needs_one_completed()
    {
        // A cancelled Uygulama task cannot be reopened once the plan is locked, so it must not block the close.
        AssertCode(ErrorCodes.ChangeOpenTasks, () => InStatus(ChangeStatus.Verifying).Close(ChangeOutcome.Successful, Done, Tally(impl: new(0, 0, 1)), Now.AddDays(2)));
        AssertCode(ErrorCodes.ChangeOpenTasks, () => InStatus(ChangeStatus.Verifying).Close(ChangeOutcome.Successful, Done, Tally(impl: new(1, 1, 1)), Now.AddDays(2)));

        var change = InStatus(ChangeStatus.Verifying);
        change.Close(ChangeOutcome.Successful, Done, Tally(impl: new(0, 1, 1)), Now.AddDays(2));

        Assert.Equal((ChangeStatus.Closed, ChangeOutcome.Successful), (change.Status, change.Outcome));
    }

    [Fact]
    public void Close_successful_is_allowed_after_the_backout_started()
    {
        var change = InStatus(ChangeStatus.Verifying);
        change.StartBackout(Now.AddDays(1));

        change.Close(ChangeOutcome.Successful, Done, Tally(backout: new(1, 0, 0)), Now.AddDays(2));

        Assert.Equal((ChangeStatus.Closed, ChangeOutcome.Successful), (change.Status, change.Outcome));
    }

    [Fact]
    public void Cancel_from_draft()
    {
        var change = NewChange();
        change.Cancel("Gerek kalmadı", Now);

        Assert.Equal((ChangeStatus.Cancelled, Now), (change.Status, change.ClosedAt));
    }

    [Fact]
    public void Close_rejects_an_end_before_the_start()
    {
        var change = InStatus(ChangeStatus.Verifying);
        AssertCode(ErrorCodes.ChangeEndBeforeStart, () => change.Close(ChangeOutcome.Successful, Done with { ActualEnd = Now }, Tally(), Now));
    }

    [Fact]
    public void Close_accepts_an_end_in_the_start_minute()
    {
        var change = NewChange();
        var start = Now.AddDays(1).AddSeconds(40);
        change.Submit(1, Now);
        change.Schedule(Now);
        change.StartImplementation(start);
        change.FinishImplementation(start);
        var minuteStart = Now.AddDays(1);

        change.Close(ChangeOutcome.Successful, Done with { ActualEnd = minuteStart }, Tally(), start);

        Assert.Equal(minuteStart, change.ActualEnd);
    }

    [Fact]
    public void Close_uses_the_given_actual_end()
    {
        var change = InStatus(ChangeStatus.Verifying);
        change.Close(ChangeOutcome.Successful, Done with { ActualEnd = Now.AddDays(1).AddHours(3) }, Tally(), Now.AddDays(5));

        Assert.Equal(Now.AddDays(1).AddHours(3), change.ActualEnd);
    }

    [Theory]
    [InlineData(ChangeOutcome.RolledBack)]
    [InlineData(ChangeOutcome.Failed)]
    public void Close_after_backout_needs_the_backout_to_be_done(ChangeOutcome outcome)
    {
        var closing = new ChangeClosing(null, "Geri alındı", null, null);
        var change = InStatus(ChangeStatus.Implementing);
        var done = Tally(backout: new(0, 1, 0));

        AssertCode(ErrorCodes.ChangeBackoutRequired, () => change.Close(outcome, closing, done, Now.AddDays(2)));
        change.StartBackout(Now.AddDays(1));
        Assert.Equal(Now.AddDays(1), change.BackoutStartedAt);
        AssertCode(ErrorCodes.ChangeBackoutRequired, () => change.Close(outcome, closing, Tally(), Now.AddDays(2)));
        AssertCode(ErrorCodes.ChangeBackoutRequired, () => change.Close(outcome, closing, Tally(backout: new(1, 1, 0)), Now.AddDays(2)));
        AssertCode(ErrorCodes.ChangeBackoutRequired, () => change.Close(outcome, closing with { BackoutNote = null }, done, Now.AddDays(2)));

        change.Close(outcome, closing, Tally(impl: new(2, 0, 0), backout: new(0, 1, 1)), Now.AddDays(2));
        Assert.Equal((ChangeStatus.Closed, outcome, "Geri alındı"), (change.Status, change.Outcome, change.BackoutNote));
    }

    [Fact]
    public void Backout_starts_once_and_from_verifying_too()
    {
        var change = InStatus(ChangeStatus.Verifying);
        change.StartBackout(Now);
        AssertInvalid(() => change.StartBackout(Now));
    }

    [Fact]
    public void Cancel_needs_a_reason_and_an_early_state()
    {
        AssertCode(ErrorCodes.ValidationFailed, () => NewChange().Cancel(" ", Now));

        var change = InStatus(ChangeStatus.Scheduled);
        change.Cancel(" Vazgeçildi ", Now);

        Assert.Equal((ChangeStatus.Cancelled, "Vazgeçildi", false), (change.Status, change.CancelReason, change.IsOpen));
        AssertInvalid(() => change.Cancel("x", Now));
    }

    [Fact]
    public void Author_and_owner_cannot_approve()
    {
        var change = NewChange();

        AssertCode(ErrorCodes.ChangeSelfApproval, () => change.RequireEligibleApprover(Author));
        AssertCode(ErrorCodes.ChangeSelfApproval, () => change.RequireEligibleApprover(Owner));
        change.RequireEligibleApprover(Guid.NewGuid());
    }
}
