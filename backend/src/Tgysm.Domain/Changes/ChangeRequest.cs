using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Changes;

/// <summary>The editable plan of a change.</summary>
public sealed record ChangeDraft(
    string Title, string? Justification, string? AffectedService, Level Risk, Level Impact,
    DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string? ImplementationPlan, string? VerificationPlan, string? BackoutPlan);

/// <summary>Closing details; <see cref="ActualEnd"/> defaults to the closing time when null.</summary>
public sealed record ChangeClosing(string? VerificationNote, string? BackoutNote, string? ClosingNote, DateTimeOffset? ActualEnd);

/// <summary>Task counts of one phase. Open means not completed and not cancelled.</summary>
public sealed record ChangePhaseTally(int Open, int Completed, int Cancelled)
{
    public int Total => Open + Completed + Cancelled;
}

/// <summary>Task counts of a change per phase, built by the caller; the closing rules read it.</summary>
public sealed record ChangeTaskTally(
    ChangePhaseTally Preparation, ChangePhaseTally Implementation, ChangePhaseTally Verification, ChangePhaseTally Backout);

/// <summary>
/// A planned modification of the IT environment (ADR-0032). States: draft → pending_approval → scheduled →
/// implementing → verifying → closed; cancel before implementation. Plan fields are locked from implementing on.
/// </summary>
public sealed class ChangeRequest : Entity
{
    public const int TitleMaxLength = 200;
    public const int AffectedServiceMaxLength = 200;
    public const int TextMaxLength = 4000;
    public const int ReasonMaxLength = 500;

    private ChangeRequest()
    {
    }

    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive search key of the title (<see cref="TurkishText.ToSearchKey"/>).</summary>
    public string SearchText { get; private set; } = string.Empty;
    public string? Justification { get; private set; }
    public string? AffectedService { get; private set; }
    public Level Risk { get; private set; }
    public Level Impact { get; private set; }
    public DateTimeOffset? WindowStart { get; private set; }
    public DateTimeOffset? WindowEnd { get; private set; }
    public string? ImplementationPlan { get; private set; }
    public string? VerificationPlan { get; private set; }
    public string? BackoutPlan { get; private set; }
    public Guid OwnerTeamId { get; private set; }
    public Guid OwnerId { get; private set; }
    public ChangeStatus Status { get; private set; }
    public ChangeOutcome? Outcome { get; private set; }
    public DateTimeOffset? ActualStart { get; private set; }
    public DateTimeOffset? ActualEnd { get; private set; }
    public DateTimeOffset? BackoutStartedAt { get; private set; }
    public string? VerificationNote { get; private set; }
    public string? BackoutNote { get; private set; }
    public string? ClosingNote { get; private set; }
    public string? CancelReason { get; private set; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsOpen => Status is not (ChangeStatus.Closed or ChangeStatus.Cancelled);

    /// <summary>From implementing on, the fingerprint fields cannot change.</summary>
    public bool IsPlanLocked => Status is ChangeStatus.Implementing or ChangeStatus.Verifying;

    public static ChangeRequest Create(
        string number, ChangeDraft draft, Guid ownerTeamId, Guid ownerId, Guid createdById, DateTimeOffset now)
    {
        var change = new ChangeRequest
        {
            Number = number,
            OwnerTeamId = ownerTeamId,
            OwnerId = ownerId,
            Status = ChangeStatus.Draft,
            CreatedById = createdById,
            CreatedAt = now,
            UpdatedAt = now,
        };
        change.Apply(draft);
        return change;
    }

    /// <summary>
    /// Replaces the plan. Returns true when a fingerprint field changed (window, risk, impact, plans), so the caller
    /// can refresh a pending approval or void an approved one.
    /// </summary>
    public bool Edit(ChangeDraft draft, DateTimeOffset now)
    {
        if (!IsOpen)
        {
            throw InvalidTransition(nameof(Edit));
        }

        if (IsPlanLocked)
        {
            throw new DomainException(ErrorCodes.ChangePlanLocked, "The plan is locked once implementation has started.");
        }

        var before = (WindowStart, WindowEnd, Risk, Impact, ImplementationPlan, VerificationPlan, BackoutPlan);
        Apply(draft);
        Touch(now);
        return before != (WindowStart, WindowEnd, Risk, Impact, ImplementationPlan, VerificationPlan, BackoutPlan);
    }

    /// <summary>Draft → pending approval. Needs a window, all three plans and at least one Uygulama task.</summary>
    public void Submit(int implementationTaskCount, DateTimeOffset now)
    {
        Require([ChangeStatus.Draft], nameof(Submit));
        RequireReady(implementationTaskCount);
        Move(ChangeStatus.PendingApproval, now);
    }

    /// <summary>The rules a submitted change keeps: a window, all three plans and at least one Uygulama task.</summary>
    public void RequireReady(int implementationTaskCount)
    {
        if (WindowStart is not { } start || WindowEnd is not { } end || start >= end)
        {
            throw NotReady("A window with a start before its end is required.");
        }

        if (ImplementationPlan is null || VerificationPlan is null || BackoutPlan is null)
        {
            throw NotReady("The implementation, verification and backout plans are required.");
        }

        if (implementationTaskCount < 1)
        {
            throw NotReady("At least one Uygulama task is required.");
        }
    }

    /// <summary>Pending approval → draft: withdrawn by the author, or returned/rejected by an approver.</summary>
    public void ReturnToDraft(DateTimeOffset now)
    {
        Require([ChangeStatus.PendingApproval], nameof(ReturnToDraft));
        Move(ChangeStatus.Draft, now);
    }

    public void Schedule(DateTimeOffset now)
    {
        Require([ChangeStatus.PendingApproval], nameof(Schedule));
        Move(ChangeStatus.Scheduled, now);
    }

    /// <summary>Scheduled → pending approval: the approval was voided by a plan edit.</summary>
    public void BackToPending(DateTimeOffset now)
    {
        Require([ChangeStatus.Scheduled], nameof(BackToPending));
        Move(ChangeStatus.PendingApproval, now);
    }

    public void StartImplementation(DateTimeOffset now)
    {
        Require([ChangeStatus.Scheduled], nameof(StartImplementation));
        ActualStart = now;
        Move(ChangeStatus.Implementing, now);
    }

    public void FinishImplementation(DateTimeOffset now)
    {
        Require([ChangeStatus.Implementing], nameof(FinishImplementation));
        Move(ChangeStatus.Verifying, now);
    }

    /// <summary>Starts the rollback; state stays implementing or verifying. Once only.</summary>
    public void StartBackout(DateTimeOffset now)
    {
        Require([ChangeStatus.Implementing, ChangeStatus.Verifying], nameof(StartBackout));
        if (BackoutStartedAt is not null)
        {
            throw InvalidTransition(nameof(StartBackout));
        }

        BackoutStartedAt = now;
        Touch(now);
    }

    /// <summary>
    /// Closes the change. Successful: from verifying, every Uygulama and Doğrulama task completed (a cancelled one
    /// does not count as completed), Hazırlık tasks completed or cancelled, a verification note. Rolled back or
    /// failed: backout started, at least one Geri dönüş task, all closed, a backout note. Open Geri dönüş tasks of a
    /// successful change are cancelled by the caller. Actual end defaults to <paramref name="now"/> when not given.
    /// </summary>
    public void Close(ChangeOutcome outcome, ChangeClosing closing, ChangeTaskTally tasks, DateTimeOffset now)
    {
        if (outcome == ChangeOutcome.Successful)
        {
            RequireSuccessful(closing, tasks);
        }
        else
        {
            RequireBackedOut(closing, tasks);
        }

        var end = closing.ActualEnd ?? now;
        if (ActualStart is { } start && end < start)
        {
            throw NotReady("The actual end cannot be before the actual start.");
        }

        Outcome = outcome;
        ActualEnd = end;
        VerificationNote = Clean(closing.VerificationNote);
        BackoutNote = Clean(closing.BackoutNote);
        ClosingNote = Clean(closing.ClosingNote);
        ClosedAt = now;
        Move(ChangeStatus.Closed, now);
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        Require([ChangeStatus.Draft, ChangeStatus.PendingApproval, ChangeStatus.Scheduled], nameof(Cancel));
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(ErrorCodes.ValidationFailed, "A cancel reason is required.");
        }

        CancelReason = reason.Trim();
        ClosedAt = now;
        Move(ChangeStatus.Cancelled, now);
    }

    /// <summary>The author and the owner never approve their own change.</summary>
    public void RequireEligibleApprover(Guid userId)
    {
        if (userId == CreatedById || userId == OwnerId)
        {
            throw new DomainException(ErrorCodes.ChangeSelfApproval, "The author and the owner cannot decide this approval.");
        }
    }

    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private void RequireSuccessful(ChangeClosing closing, ChangeTaskTally tasks)
    {
        Require([ChangeStatus.Verifying], nameof(Close));
        if (tasks.Implementation is not { Open: 0, Cancelled: 0 } || tasks.Verification is not { Open: 0, Cancelled: 0 } || tasks.Preparation.Open != 0)
        {
            throw new DomainException(ErrorCodes.ChangeOpenTasks, "Every Uygulama and Doğrulama task must be completed and no Hazırlık task open.");
        }

        if (string.IsNullOrWhiteSpace(closing.VerificationNote))
        {
            throw new DomainException(ErrorCodes.ChangeVerificationRequired, "A verification note is required.");
        }
    }

    private void RequireBackedOut(ChangeClosing closing, ChangeTaskTally tasks)
    {
        Require([ChangeStatus.Implementing, ChangeStatus.Verifying], nameof(Close));
        if (BackoutStartedAt is null || tasks.Backout.Total < 1 || tasks.Backout.Open != 0 || string.IsNullOrWhiteSpace(closing.BackoutNote))
        {
            throw new DomainException(
                ErrorCodes.ChangeBackoutRequired, "Start the backout, finish at least one Geri dönüş task and write a backout note.");
        }
    }

    private void Apply(ChangeDraft draft)
    {
        Title = draft.Title.Trim();
        SearchText = TurkishText.ToSearchKey(draft.Title);
        Justification = Clean(draft.Justification);
        AffectedService = Clean(draft.AffectedService);
        Risk = draft.Risk;
        Impact = draft.Impact;
        WindowStart = draft.WindowStart;
        WindowEnd = draft.WindowEnd;
        ImplementationPlan = Clean(draft.ImplementationPlan);
        VerificationPlan = Clean(draft.VerificationPlan);
        BackoutPlan = Clean(draft.BackoutPlan);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Move(ChangeStatus status, DateTimeOffset now)
    {
        Status = status;
        Touch(now);
    }

    private void Require(ChangeStatus[] allowed, string operation)
    {
        if (!allowed.Contains(Status))
        {
            throw InvalidTransition(operation);
        }
    }

    private DomainException InvalidTransition(string operation) =>
        new(ErrorCodes.ChangeInvalidTransition, $"'{operation}' is not allowed while the change is {Status}.");

    private static DomainException NotReady(string message) => new(ErrorCodes.ChangeNotReady, message);
}
