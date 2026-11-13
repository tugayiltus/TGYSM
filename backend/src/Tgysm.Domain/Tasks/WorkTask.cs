using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tasks;

/// <summary>
/// Work handed to a team, standalone or linked to one ticket (ADR-0031). The link is fixed after creation, and no
/// task action changes the linked ticket. States: open → in_progress ⇄ on_hold → completed | cancelled; reopen.
/// A due date is not an SLA.
/// </summary>
public sealed class WorkTask : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int NoteMaxLength = 2000;
    public const int ReasonMaxLength = 500;

    private WorkTask()
    {
    }

    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Turkish-insensitive search key of the title (<see cref="TurkishText.ToSearchKey"/>).</summary>
    public string SearchText { get; private set; } = string.Empty;
    public Guid TeamId { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public WorkTaskStatus Status { get; private set; }
    public Priority Priority { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public Guid? TicketId { get; private init; }
    public HoldReason? HoldReason { get; private set; }
    public string? HoldNote { get; private set; }
    public string? ResultNote { get; private set; }
    public string? CancelReason { get; private set; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsOpen => Status is not (WorkTaskStatus.Completed or WorkTaskStatus.Cancelled);

    public static WorkTask Create(
        string number, string title, string? description, Guid teamId, Guid? assigneeId, Priority priority, DateOnly? dueDate,
        Guid? ticketId, Guid createdById, DateTimeOffset now) =>
        new()
        {
            Number = number,
            Title = title.Trim(),
            SearchText = TurkishText.ToSearchKey(title),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            TeamId = teamId,
            AssigneeId = assigneeId,
            Status = WorkTaskStatus.Open,
            Priority = priority,
            DueDate = dueDate,
            TicketId = ticketId,
            CreatedById = createdById,
            CreatedAt = now,
            UpdatedAt = now,
        };

    /// <summary>Overdue: the due date has passed in the company calendar and the task is still open.</summary>
    public bool IsOverdue(DateOnly today) => IsOpen && DueDate is { } due && due < today;

    /// <summary>
    /// Sets or clears the assignee inside the same team; membership is checked by the caller.
    /// Clearing the assignee of running work returns it to open, so a member can claim and start it again.
    /// </summary>
    public TaskAssignment Assign(Guid? assigneeId, DateTimeOffset now, Guid actorUserId)
    {
        RequireOpen(nameof(Assign));
        var record = TaskAssignment.Record(Id, TeamId, TeamId, AssigneeId, assigneeId, null, null, actorUserId, now);
        AssigneeId = assigneeId;
        if (assigneeId is null && Status == WorkTaskStatus.InProgress)
        {
            Status = WorkTaskStatus.Open;
        }

        Touch(now);
        return record;
    }

    /// <summary>Moves the task to another team; the new team picks its own assignee, so work restarts from open.</summary>
    public TaskAssignment Transfer(Guid teamId, TransferReason reason, string? note, DateTimeOffset now, Guid actorUserId)
    {
        RequireOpen(nameof(Transfer));
        if (teamId == TeamId)
        {
            throw InvalidTransition(nameof(Transfer));
        }

        var record = TaskAssignment.Record(Id, TeamId, teamId, AssigneeId, null, reason, note, actorUserId, now);
        TeamId = teamId;
        AssigneeId = null;
        if (Status == WorkTaskStatus.InProgress)
        {
            Status = WorkTaskStatus.Open;
        }

        Touch(now);
        return record;
    }

    public void Start(DateTimeOffset now)
    {
        Require([WorkTaskStatus.Open], nameof(Start));
        Status = WorkTaskStatus.InProgress;
        Touch(now);
    }

    public void Hold(HoldReason reason, string? note, DateTimeOffset now)
    {
        Require([WorkTaskStatus.Open, WorkTaskStatus.InProgress], nameof(Hold));
        if (reason == Tickets.HoldReason.ManagerApproval)
        {
            // Manager approval belongs to requests, not tasks.
            throw InvalidTransition(nameof(Hold));
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (reason == Tickets.HoldReason.Other && trimmed is null)
        {
            throw new DomainException(ErrorCodes.TaskHoldNoteRequired, "Hold reason 'other' needs a note.");
        }

        Status = WorkTaskStatus.OnHold;
        HoldReason = reason;
        HoldNote = trimmed;
        Touch(now);
    }

    public void Resume(DateTimeOffset now)
    {
        Require([WorkTaskStatus.OnHold], nameof(Resume));
        Status = AssigneeId is null ? WorkTaskStatus.Open : WorkTaskStatus.InProgress;
        ClearHold();
        Touch(now);
    }

    public void Complete(string resultNote, DateTimeOffset now)
    {
        Require([WorkTaskStatus.Open, WorkTaskStatus.InProgress], nameof(Complete));
        if (string.IsNullOrWhiteSpace(resultNote))
        {
            throw new DomainException(ErrorCodes.TaskResultRequired, "A result note is required.");
        }

        Status = WorkTaskStatus.Completed;
        ResultNote = resultNote.Trim();
        Close(now);
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        RequireOpen(nameof(Cancel));
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(ErrorCodes.TaskCancelReasonRequired, "A cancel reason is required.");
        }

        Status = WorkTaskStatus.Cancelled;
        CancelReason = reason.Trim();
        ClearHold();
        Close(now);
    }

    public void Reopen(DateTimeOffset now)
    {
        if (IsOpen)
        {
            throw InvalidTransition(nameof(Reopen));
        }

        Status = WorkTaskStatus.Open;
        ResultNote = null;
        CancelReason = null;
        ClosedAt = null;
        Touch(now);
    }

    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private void Close(DateTimeOffset now)
    {
        ClosedAt = now;
        Touch(now);
    }

    private void ClearHold()
    {
        HoldReason = null;
        HoldNote = null;
    }

    private void RequireOpen(string operation)
    {
        if (!IsOpen)
        {
            throw InvalidTransition(operation);
        }
    }

    private void Require(WorkTaskStatus[] allowed, string operation)
    {
        if (!allowed.Contains(Status))
        {
            throw InvalidTransition(operation);
        }
    }

    private DomainException InvalidTransition(string operation) =>
        new(ErrorCodes.TaskInvalidTransition, $"'{operation}' is not allowed while the task is {Status}.");
}
