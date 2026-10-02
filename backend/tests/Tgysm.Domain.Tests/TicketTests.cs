using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests;

public sealed class TicketTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Team = Guid.NewGuid();
    private static readonly Guid OtherTeam = Guid.NewGuid();
    private static readonly Guid Agent = Guid.NewGuid();
    private static readonly Guid RequestType = Guid.NewGuid();

    private static Ticket Incident() =>
        Ticket.CreateIncident("INC-2026-000001", " Yazıcı çalışmıyor ", "Kağıt sıkışıyor", null, null, Requester, Team, Now);

    private static Ticket Request(bool requiresApproval) =>
        Ticket.CreateRequest("REQ-2026-000001", "Yeni dizüstü", "Göreve başlıyorum", null, null, Requester, Team, RequestType, requiresApproval, Now);

    private static void AssertInvalid(Action action) =>
        Assert.Equal(ErrorCodes.TicketInvalidTransition, Assert.Throws<DomainException>(action).Code);

    [Fact]
    public void New_incident_is_open_with_medium_impact_and_urgency()
    {
        var ticket = Incident();

        Assert.Equal(TicketKind.Incident, ticket.Kind);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal("Yazıcı çalışmıyor", ticket.Subject);
        Assert.Equal((Level.Medium, Level.Medium, Priority.P3), (ticket.Impact, ticket.Urgency, ticket.Priority));
        Assert.Null(ticket.RequestTypeId);
        Assert.Null(ticket.AssigneeId);
    }

    [Fact]
    public void Request_needing_approval_starts_on_hold_for_manager_approval()
    {
        var ticket = Request(requiresApproval: true);

        Assert.Equal(TicketStatus.OnHold, ticket.Status);
        Assert.Equal(HoldReason.ManagerApproval, ticket.HoldReason);
        Assert.True(ticket.RequiresApproval);
        Assert.Equal(TicketStatus.Open, Request(requiresApproval: false).Status);
    }

    [Fact]
    public void Work_moves_through_start_hold_resume_and_complete()
    {
        var ticket = Incident();
        ticket.Assign(Team, Agent, Now);

        ticket.Start(Now);
        ticket.Hold(HoldReason.Vendor, null, Now);
        Assert.Equal((TicketStatus.OnHold, HoldReason.Vendor), (ticket.Status, ticket.HoldReason));
        ticket.Resume(Now);
        Assert.Equal((TicketStatus.InProgress, (HoldReason?)null), (ticket.Status, ticket.HoldReason));
        ticket.Complete(" Toner değişti ", TicketOutcome.Resolved, hasRelatedRequest: false, isApproved: false, Now.AddHours(1));

        Assert.Equal(TicketStatus.Completed, ticket.Status);
        Assert.Equal("Toner değişti", ticket.Resolution);
        Assert.Equal(TicketOutcome.Resolved, ticket.Outcome);
        Assert.Equal(Now.AddHours(1), ticket.CompletedAt);
    }

    [Fact]
    public void Resume_without_assignee_goes_back_to_open()
    {
        var ticket = Incident();
        ticket.Hold(HoldReason.User, null, Now);

        ticket.Resume(Now);

        Assert.Equal(TicketStatus.Open, ticket.Status);
    }

    [Fact]
    public void Hold_reason_other_needs_a_note_and_manager_approval_is_reserved()
    {
        var ticket = Incident();

        Assert.Equal(ErrorCodes.TicketHoldNoteRequired, Assert.Throws<DomainException>(() => ticket.Hold(HoldReason.Other, " ", Now)).Code);
        AssertInvalid(() => ticket.Hold(HoldReason.ManagerApproval, null, Now));
        ticket.Hold(HoldReason.Other, "Kullanıcı izinde", Now);
        Assert.Equal("Kullanıcı izinde", ticket.HoldNote);
        AssertInvalid(() => ticket.Hold(HoldReason.Vendor, null, Now));
    }

    [Fact]
    public void Approval_hold_is_left_only_through_the_approval()
    {
        var ticket = Request(requiresApproval: true);

        Assert.Equal(ErrorCodes.TicketApprovalPending, Assert.Throws<DomainException>(() => ticket.Resume(Now)).Code);
        ticket.ApprovalGranted(Now);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        ticket.ReturnToApproval(Now);
        Assert.Equal((TicketStatus.OnHold, HoldReason.ManagerApproval), (ticket.Status, ticket.HoldReason));
    }

    [Fact]
    public void Rejected_approval_cancels_the_request()
    {
        var ticket = Request(requiresApproval: true);

        ticket.ApprovalRejected("Bütçe yok", Now);

        Assert.Equal(TicketStatus.Cancelled, ticket.Status);
        Assert.Equal(TicketOutcome.ApprovalRejected, ticket.Outcome);
        Assert.Equal("Bütçe yok", ticket.CancelReason);
        Assert.Equal(Now, ticket.CompletedAt);
        Assert.Null(ticket.HoldReason);
    }

    [Fact]
    public void Complete_needs_a_resolution_and_an_incident_outcome()
    {
        var ticket = Incident();

        Assert.Equal(ErrorCodes.TicketResolutionRequired, Assert.Throws<DomainException>(() => ticket.Complete(" ", TicketOutcome.Resolved, false, false, Now)).Code);
        Assert.Equal(ErrorCodes.TicketOutcomeRequired, Assert.Throws<DomainException>(() => ticket.Complete("Tamam", null, false, false, Now)).Code);
        Assert.Equal(ErrorCodes.TicketOutcomeRequired, Assert.Throws<DomainException>(() => ticket.Complete("Tamam", TicketOutcome.Fulfilled, false, false, Now)).Code);
        Assert.Equal(ErrorCodes.TicketRelatedRequestRequired, Assert.Throws<DomainException>(() => ticket.Complete("Talep açılmalı", TicketOutcome.ServiceRequestRequired, false, false, Now)).Code);
        ticket.Complete("Talep açıldı", TicketOutcome.ServiceRequestRequired, hasRelatedRequest: true, isApproved: false, Now);
        Assert.Equal(TicketOutcome.ServiceRequestRequired, ticket.Outcome);
    }

    [Fact]
    public void Request_needing_approval_cannot_complete_before_it_is_approved()
    {
        var ticket = Request(requiresApproval: true);
        ticket.ApprovalGranted(Now);

        Assert.Equal(ErrorCodes.TicketApprovalRequired, Assert.Throws<DomainException>(() => ticket.Complete("Teslim edildi", null, false, isApproved: false, Now)).Code);
        ticket.Complete("Teslim edildi", null, false, isApproved: true, Now);

        Assert.Equal(TicketOutcome.Fulfilled, ticket.Outcome);
    }

    [Fact]
    public void On_hold_ticket_cannot_be_completed_and_closed_tickets_do_not_change()
    {
        var ticket = Incident();
        ticket.Hold(HoldReason.Vendor, null, Now);
        AssertInvalid(() => ticket.Complete("Tamam", TicketOutcome.Resolved, false, false, Now));

        ticket.Cancel(" Mükerrer kayıt ", Now);

        Assert.Equal((TicketStatus.Cancelled, "Mükerrer kayıt"), (ticket.Status, ticket.CancelReason));
        Assert.Equal(Now, ticket.CompletedAt); // the close date reports use
        AssertInvalid(() => ticket.Cancel("Tekrar", Now));
        AssertInvalid(() => ticket.Resume(Now));
        AssertInvalid(() => ticket.Assign(Team, Agent, Now));
        AssertInvalid(() => ticket.ChangePriority(Level.High, Level.High, Now));
        AssertInvalid(() => ticket.Start(Now));
    }

    [Fact]
    public void Cancel_needs_a_reason()
    {
        var ticket = Incident();

        Assert.Equal(ErrorCodes.TicketCancelReasonRequired, Assert.Throws<DomainException>(() => ticket.Cancel("  ", Now)).Code);
    }

    [Fact]
    public void Assign_moves_team_and_assignee_and_returns_the_history_row()
    {
        var ticket = Incident();

        var first = ticket.Assign(Team, Agent, Now);
        var second = ticket.Assign(OtherTeam, null, Now);

        Assert.Equal((Team, (Guid?)null, Team, (Guid?)Agent), (first.FromTeamId, first.FromAssigneeId, first.ToTeamId, first.ToAssigneeId));
        Assert.Equal((OtherTeam, (Guid?)null), (ticket.TeamId, ticket.AssigneeId));
        Assert.Equal(Agent, second.FromAssigneeId);
    }

    [Fact]
    public void Priority_follows_the_matrix_and_reports_the_change()
    {
        var ticket = Incident();

        var (from, to) = ticket.ChangePriority(Level.High, Level.High, Now);

        Assert.Equal((Priority.P3, Priority.P1), (from, to));
        Assert.Equal(Priority.P1, ticket.Priority);
    }

    [Theory]
    [InlineData(Level.High, Level.High, Priority.P1)]
    [InlineData(Level.High, Level.Medium, Priority.P2)]
    [InlineData(Level.High, Level.Low, Priority.P3)]
    [InlineData(Level.Medium, Level.High, Priority.P2)]
    [InlineData(Level.Medium, Level.Medium, Priority.P3)]
    [InlineData(Level.Medium, Level.Low, Priority.P4)]
    [InlineData(Level.Low, Level.High, Priority.P3)]
    [InlineData(Level.Low, Level.Medium, Priority.P4)]
    [InlineData(Level.Low, Level.Low, Priority.P4)]
    public void Priority_matrix_matches_the_spec(Level impact, Level urgency, Priority expected) =>
        Assert.Equal(expected, PriorityMatrix.Calculate(impact, urgency));

    [Theory]
    [InlineData(TicketKind.Incident, 2026, 1, "INC-2026-000001")]
    [InlineData(TicketKind.Request, 2027, 123456, "REQ-2027-123456")]
    [InlineData(TicketKind.Request, 2027, 1234567, "REQ-2027-1234567")]
    public void Ticket_numbers_have_kind_year_and_six_digits(TicketKind kind, int year, long value, string expected) =>
        Assert.Equal(expected, TicketNumber.Format(kind, year, value));

    [Fact]
    public void Ticket_number_parse_accepts_any_case() =>
        Assert.True(TicketNumber.IsValid("inc-2026-000001") && !TicketNumber.IsValid("INC-26-1") && !TicketNumber.IsValid("XYZ-2026-000001"));
}
