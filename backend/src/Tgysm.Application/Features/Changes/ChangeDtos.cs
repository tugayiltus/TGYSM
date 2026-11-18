using Tgysm.Domain.Assets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Changes;

public sealed record ChangeAssetItem(Guid Id, string AssetTag, AssetType Type, string Brand, string Model, AssetStatus Status);

public sealed record ChangeTaskRow(
    Guid Id, string Number, string Title, WorkTaskStatus Status, ChangePhase Phase, Guid TeamId, string TeamName, string? AssigneeName);

/// <summary>The tasks of one phase; every phase is present, possibly empty.</summary>
public sealed record ChangePhaseGroup(ChangePhase Phase, IReadOnlyList<ChangeTaskRow> Tasks);

/// <param name="CanDecide">The viewer is an eligible approver and the approval waits.</param>
public sealed record ChangeApprovalInfo(
    ChangeApprovalStatus Status, Guid? DecidedById, string? DecidedByName, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, bool CanDecide, uint Version);

/// <summary>Full change view for admins and owner team members.</summary>
public sealed record ChangeDetail(
    Guid Id,
    string Number,
    string Title,
    string? Justification,
    string? AffectedService,
    Level Risk,
    Level Impact,
    DateTimeOffset? WindowStart,
    DateTimeOffset? WindowEnd,
    string? ImplementationPlan,
    string? VerificationPlan,
    string? BackoutPlan,
    Guid OwnerTeamId,
    string OwnerTeamName,
    Guid OwnerId,
    string OwnerName,
    ChangeStatus Status,
    ChangeOutcome? Outcome,
    DateTimeOffset? ActualStart,
    DateTimeOffset? ActualEnd,
    DateTimeOffset? BackoutStartedAt,
    string? VerificationNote,
    string? BackoutNote,
    string? ClosingNote,
    string? CancelReason,
    Guid CreatedById,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<ChangeAssetItem> Assets,
    IReadOnlyList<ChangePhaseGroup> TasksByPhase,
    ChangeApprovalInfo? Approval,
    IReadOnlyList<ChangeConflict> Conflicts,
    bool HasEligibleApprover,
    bool CanWork,
    uint Version);

/// <summary>
/// Working summary for people who only have a task on the change: the plan of their phase, never approvals, history,
/// assets or other teams' tasks.
/// </summary>
public sealed record ChangeSummary(
    Guid Id,
    string Number,
    string Title,
    ChangeStatus Status,
    DateTimeOffset? WindowStart,
    DateTimeOffset? WindowEnd,
    Level Risk,
    string? ImplementationPlan,
    string? BackoutPlan);

/// <summary>Exactly one of <see cref="Detail"/> and <see cref="Summary"/> is set, matching <see cref="Access"/>.</summary>
public sealed record ChangeView(ChangeAccess Access, ChangeDetail? Detail, ChangeSummary? Summary);
