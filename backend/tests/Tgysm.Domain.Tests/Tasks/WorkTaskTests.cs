using Tgysm.Domain.Common;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests.Tasks;

public sealed class WorkTaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Team = Guid.NewGuid();
    private static readonly Guid OtherTeam = Guid.NewGuid();
    private static readonly Guid Agent = Guid.NewGuid();
    private static readonly Guid Creator = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 13);

    private static WorkTask NewTask(Guid? assignee = null, DateOnly? due = null, Guid? ticketId = null) =>
        WorkTask.Create("TSK-2026-000001", " Yedek al ", " Veritabanı yedeği ", Team, assignee, Priority.P2, due, ticketId, Creator, Now);

    private static void AssertCode(string code, Action action) =>
        Assert.Equal(code, Assert.Throws<DomainException>(action).Code);

    private static void AssertInvalid(Action action) => AssertCode(ErrorCodes.TaskInvalidTransition, action);

    [Fact]
    public void New_task_is_open_with_trimmed_text_and_its_link()
    {
        var ticketId = Guid.NewGuid();
        var task = NewTask(ticketId: ticketId);

        Assert.Equal(WorkTaskStatus.Open, task.Status);
        Assert.Equal("Yedek al", task.Title);
        Assert.Equal("Veritabanı yedeği", task.Description);
        Assert.Equal(ticketId, task.TicketId);
        Assert.Equal((Team, Priority.P2, Creator), (task.TeamId, task.Priority, task.CreatedById));
        Assert.True(task.IsOpen);
    }

    [Fact]
    public void Start_hold_resume_complete_runs_the_normal_path()
    {
        var task = NewTask(Agent);

        task.Start(Now);
        Assert.Equal(WorkTaskStatus.InProgress, task.Status);
        task.Hold(HoldReason.Vendor, null, Now);
        Assert.Equal((WorkTaskStatus.OnHold, HoldReason.Vendor), (task.Status, task.HoldReason));
        task.Resume(Now);
        Assert.Equal(WorkTaskStatus.InProgress, task.Status);
        Assert.Null(task.HoldReason);
        task.Complete(" Yedek alındı ", Now);

        Assert.Equal(WorkTaskStatus.Completed, task.Status);
        Assert.Equal("Yedek alındı", task.ResultNote);
        Assert.Equal(Now, task.ClosedAt);
        Assert.False(task.IsOpen);
    }

    [Fact]
    public void Resume_without_assignee_returns_to_open()
    {
        var task = NewTask();
        task.Hold(HoldReason.ThirdParty, null, Now);

        task.Resume(Now);

        Assert.Equal(WorkTaskStatus.Open, task.Status);
    }

    [Fact]
    public void Complete_needs_a_result_note()
    {
        var task = NewTask(Agent);
        task.Start(Now);

        AssertCode(ErrorCodes.TaskResultRequired, () => task.Complete("  ", Now));
    }

    [Fact]
    public void Cancel_needs_a_reason_and_closes_the_task()
    {
        var task = NewTask();

        AssertCode(ErrorCodes.TaskCancelReasonRequired, () => task.Cancel(" ", Now));
        task.Cancel("Gerek kalmadı", Now);

        Assert.Equal((WorkTaskStatus.Cancelled, "Gerek kalmadı"), (task.Status, task.CancelReason));
    }

    [Fact]
    public void Hold_other_needs_a_note_and_manager_approval_is_refused()
    {
        var task = NewTask();

        AssertCode(ErrorCodes.TaskHoldNoteRequired, () => task.Hold(HoldReason.Other, null, Now));
        AssertInvalid(() => task.Hold(HoldReason.ManagerApproval, "x", Now));
    }

    [Fact]
    public void Refused_transitions()
    {
        var task = NewTask(Agent);
        AssertInvalid(() => task.Resume(Now));
        task.Start(Now);
        AssertInvalid(() => task.Start(Now));
        AssertInvalid(() => task.Reopen(Now));
        task.Hold(HoldReason.User, null, Now);
        AssertInvalid(() => task.Complete("Bitti", Now));
        task.Resume(Now);
        task.Complete("Bitti", Now);
        AssertInvalid(() => task.Start(Now));
        AssertInvalid(() => task.Hold(HoldReason.User, null, Now));
        AssertInvalid(() => task.Cancel("x", Now));
        AssertInvalid(() => task.Assign(Agent, Now, Creator));
        AssertInvalid(() => task.Transfer(OtherTeam, TransferReason.Workload, null, Now, Creator));
    }

    [Fact]
    public void Reopen_clears_the_closing_fields()
    {
        var task = NewTask(Agent);
        task.Start(Now);
        task.Complete("Bitti", Now);

        task.Reopen(Now.AddHours(1));

        Assert.Equal(WorkTaskStatus.Open, task.Status);
        Assert.Null(task.ResultNote);
        Assert.Null(task.ClosedAt);
        Assert.Equal(Agent, task.AssigneeId);
    }

    [Fact]
    public void Assign_records_history()
    {
        var task = NewTask();

        var record = task.Assign(Agent, Now, Creator);

        Assert.Equal(Agent, task.AssigneeId);
        Assert.Equal((task.Id, Team, Team, (Guid?)null, (Guid?)Agent), (record.TaskId, record.FromTeamId, record.ToTeamId, record.FromAssigneeId, record.ToAssigneeId));
    }

    [Fact]
    public void Clearing_the_assignee_of_running_work_returns_to_open()
    {
        var task = NewTask(Agent);
        task.Start(Now);

        task.Assign(null, Now, Creator);

        Assert.Equal(((Guid?)null, WorkTaskStatus.Open), (task.AssigneeId, task.Status));
    }

    [Fact]
    public void Handing_running_work_to_another_member_keeps_it_in_progress()
    {
        var task = NewTask(Agent);
        task.Start(Now);

        task.Assign(Creator, Now, Creator);

        Assert.Equal(((Guid?)Creator, WorkTaskStatus.InProgress), (task.AssigneeId, task.Status));
    }

    [Fact]
    public void Transfer_moves_team_clears_assignee_and_reopens_work()
    {
        var task = NewTask(Agent);
        task.Start(Now);

        var record = task.Transfer(OtherTeam, TransferReason.OtherTeamResponsibility, " DB ekibi ", Now, Creator);

        Assert.Equal((OtherTeam, (Guid?)null, WorkTaskStatus.Open), (task.TeamId, task.AssigneeId, task.Status));
        Assert.Equal((TransferReason.OtherTeamResponsibility, "DB ekibi"), (record.Reason, record.Note));
    }

    [Fact]
    public void Transfer_to_the_same_team_is_refused()
    {
        var task = NewTask();

        AssertInvalid(() => task.Transfer(Team, TransferReason.Workload, null, Now, Creator));
    }

    [Theory]
    [InlineData(-1, false, true)]
    [InlineData(0, false, false)]
    [InlineData(-1, true, false)]
    public void Overdue_when_due_date_passed_and_task_open(int dueOffsetDays, bool closed, bool expected)
    {
        var task = NewTask(Agent, Today.AddDays(dueOffsetDays));
        if (closed)
        {
            task.Cancel("x", Now);
        }

        Assert.Equal(expected, task.IsOverdue(Today));
    }

    [Fact]
    public void Task_without_due_date_is_never_overdue() => Assert.False(NewTask().IsOverdue(Today));
}
