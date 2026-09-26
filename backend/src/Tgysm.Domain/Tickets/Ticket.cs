using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tickets;

/// <summary>
/// Incident or request in one table with kind-specific columns (ADR-0009). State machine (spec §3.3):
/// open → in_progress → completed; open/in_progress ↔ on_hold; open/in_progress/on_hold → cancelled. No reopen.
/// </summary>
public sealed class Ticket : Entity
{
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int TextMaxLength = 2000;
    public const int ReasonMaxLength = 500;

    private Ticket()
    {
    }

    public string Number { get; private set; } = string.Empty;
    public TicketKind Kind { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive search key of the subject (<see cref="TurkishText.ToSearchKey"/>).</summary>
    public string SearchText { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public Guid? LocationId { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public TicketStatus Status { get; private set; }
    public HoldReason? HoldReason { get; private set; }
    public string? HoldNote { get; private set; }
    public Level Impact { get; private set; }
    public Level Urgency { get; private set; }
    public Priority Priority { get; private set; }
    public Guid? RequestTypeId { get; private set; }
    public bool RequiresApproval { get; private set; }
    public string? RootCause { get; private set; }
    public string? Resolution { get; private set; }
    public TicketOutcome? Outcome { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsClosed => Status is TicketStatus.Completed or TicketStatus.Cancelled;

    public static Ticket CreateIncident(
        string number, string subject, string description, Guid? categoryId, Guid? locationId, Guid requesterId, Guid teamId, DateTimeOffset now) =>
        New(number, TicketKind.Incident, subject, description, categoryId, locationId, requesterId, teamId, now);

    public static Ticket CreateRequest(
        string number, string subject, string description, Guid? categoryId, Guid? locationId, Guid requesterId, Guid teamId,
        Guid requestTypeId, bool requiresApproval, DateTimeOffset now)
    {
        var ticket = New(number, TicketKind.Request, subject, description, categoryId, locationId, requesterId, teamId, now);
        ticket.RequestTypeId = requestTypeId;
        ticket.RequiresApproval = requiresApproval;
        if (requiresApproval)
        {
            ticket.Status = TicketStatus.OnHold;
            ticket.HoldReason = Tickets.HoldReason.ManagerApproval;
        }

        return ticket;
    }

    private static Ticket New(
        string number, TicketKind kind, string subject, string description, Guid? categoryId, Guid? locationId, Guid requesterId, Guid teamId, DateTimeOffset now) =>
        new()
        {
            Number = number,
            Kind = kind,
            Subject = subject.Trim(),
            SearchText = TurkishText.ToSearchKey(subject),
            Description = description.Trim(),
            CategoryId = categoryId,
            LocationId = locationId,
            RequesterId = requesterId,
            TeamId = teamId,
            Status = TicketStatus.Open,
            Impact = Level.Medium,
            Urgency = Level.Medium,
            Priority = PriorityMatrix.Calculate(Level.Medium, Level.Medium),
            CreatedAt = now,
            UpdatedAt = now,
        };

    /// <summary>Claim or transfer: sets team and assignee and returns the history row.</summary>
    public TicketAssignment Assign(Guid teamId, Guid? assigneeId, DateTimeOffset now, Guid actorUserId = default, TransferReason? reason = null, string? note = null)
    {
        RequireOpenForWork(nameof(Assign));
        var record = TicketAssignment.Record(Id, TeamId, teamId, AssigneeId, assigneeId, reason, note, actorUserId, now);
        TeamId = teamId;
        AssigneeId = assigneeId;
        Touch(now);
        return record;
    }

    public void Start(DateTimeOffset now)
    {
        Require([TicketStatus.Open], nameof(Start));
        Status = TicketStatus.InProgress;
        Touch(now);
    }

    public void Hold(HoldReason reason, string? note, DateTimeOffset now)
    {
        Require([TicketStatus.Open, TicketStatus.InProgress], nameof(Hold));
        if (reason == Tickets.HoldReason.ManagerApproval)
        {
            // Approval holds are owned by the approval flow.
            throw InvalidTransition(nameof(Hold));
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (reason == Tickets.HoldReason.Other && trimmed is null)
        {
            throw new DomainException(ErrorCodes.TicketHoldNoteRequired, "Hold reason 'other' needs a note.");
        }

        Status = TicketStatus.OnHold;
        HoldReason = reason;
        HoldNote = trimmed;
        Touch(now);
    }

    public void Resume(DateTimeOffset now)
    {
        Require([TicketStatus.OnHold], nameof(Resume));
        if (HoldReason == Tickets.HoldReason.ManagerApproval)
        {
            throw new DomainException(ErrorCodes.TicketApprovalPending, "The ticket waits for its approval.");
        }

        LeaveHold(now);
    }

    public void ApprovalGranted(DateTimeOffset now)
    {
        RequireApprovalHold(nameof(ApprovalGranted));
        Status = TicketStatus.Open;
        ClearHold();
        Touch(now);
    }

    public void ApprovalRejected(string reason, DateTimeOffset now)
    {
        RequireApprovalHold(nameof(ApprovalRejected));
        Status = TicketStatus.Cancelled;
        Outcome = TicketOutcome.ApprovalRejected;
        CancelReason = reason.Trim();
        ClearHold();
        Touch(now);
    }

    /// <summary>An approved approval was voided after a scope change: the request waits for a new decision.</summary>
    public void ReturnToApproval(DateTimeOffset now)
    {
        Require([TicketStatus.Open, TicketStatus.InProgress, TicketStatus.OnHold], nameof(ReturnToApproval));
        Status = TicketStatus.OnHold;
        HoldReason = Tickets.HoldReason.ManagerApproval;
        HoldNote = null;
        Touch(now);
    }

    public void Complete(string resolution, TicketOutcome? outcome, bool hasRelatedRequest, bool isApproved, DateTimeOffset now, string? rootCause = null)
    {
        Require([TicketStatus.Open, TicketStatus.InProgress], nameof(Complete));
        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new DomainException(ErrorCodes.TicketResolutionRequired, "A resolution is required.");
        }

        if (Kind == TicketKind.Incident)
        {
            if (outcome is not (TicketOutcome.Resolved or TicketOutcome.ServiceRequestRequired))
            {
                throw new DomainException(ErrorCodes.TicketOutcomeRequired, "An incident needs the outcome resolved or service_request_required.");
            }

            if (outcome == TicketOutcome.ServiceRequestRequired && !hasRelatedRequest)
            {
                throw new DomainException(ErrorCodes.TicketRelatedRequestRequired, "Create the related request first.");
            }
        }
        else
        {
            if (RequiresApproval && !isApproved)
            {
                throw new DomainException(ErrorCodes.TicketApprovalRequired, "The request is not approved.");
            }

            outcome = TicketOutcome.Fulfilled;
        }

        Status = TicketStatus.Completed;
        Resolution = resolution.Trim();
        RootCause = string.IsNullOrWhiteSpace(rootCause) ? null : rootCause.Trim();
        Outcome = outcome;
        CompletedAt = now;
        Touch(now);
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        Require([TicketStatus.Open, TicketStatus.InProgress, TicketStatus.OnHold], nameof(Cancel));
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(ErrorCodes.TicketCancelReasonRequired, "A cancel reason is required.");
        }

        Status = TicketStatus.Cancelled;
        CancelReason = reason.Trim();
        ClearHold();
        Touch(now);
    }

    public (Priority From, Priority To) ChangePriority(Level impact, Level urgency, DateTimeOffset now)
    {
        RequireOpenForWork(nameof(ChangePriority));
        var from = Priority;
        Impact = impact;
        Urgency = urgency;
        Priority = PriorityMatrix.Calculate(impact, urgency);
        Touch(now);
        return (from, Priority);
    }

    /// <summary>Marks a change that is not a state change (comment, link) so lists sort by activity.</summary>
    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private void LeaveHold(DateTimeOffset now)
    {
        Status = AssigneeId is null ? TicketStatus.Open : TicketStatus.InProgress;
        ClearHold();
        Touch(now);
    }

    private void ClearHold()
    {
        HoldReason = null;
        HoldNote = null;
    }

    private void RequireApprovalHold(string operation)
    {
        if (Status != TicketStatus.OnHold || HoldReason != Tickets.HoldReason.ManagerApproval)
        {
            throw InvalidTransition(operation);
        }
    }

    private void RequireOpenForWork(string operation)
    {
        if (IsClosed)
        {
            throw InvalidTransition(operation);
        }
    }

    private void Require(TicketStatus[] allowed, string operation)
    {
        if (!allowed.Contains(Status))
        {
            throw InvalidTransition(operation);
        }
    }

    private DomainException InvalidTransition(string operation) =>
        new(ErrorCodes.TicketInvalidTransition, $"'{operation}' is not allowed while the ticket is {Status}.");
}
