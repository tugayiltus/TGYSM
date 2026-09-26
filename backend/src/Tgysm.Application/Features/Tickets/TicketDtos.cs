using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

public sealed record TicketListItem(
    string Number,
    TicketKind Kind,
    string Subject,
    TicketStatus Status,
    HoldReason? HoldReason,
    Priority Priority,
    string TeamName,
    string? AssigneeName,
    string RequesterName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ApprovalSummary(Guid Id, ApprovalStatus Status, Guid? ApproverId, string? ApproverName, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);

public sealed record TicketDetail(
    Guid Id,
    string Number,
    TicketKind Kind,
    string Subject,
    string Description,
    TicketStatus Status,
    HoldReason? HoldReason,
    string? HoldNote,
    Level Impact,
    Level Urgency,
    Priority Priority,
    Guid? CategoryId,
    string? CategoryName,
    Guid? LocationId,
    string? LocationName,
    Guid RequesterId,
    string RequesterName,
    Guid TeamId,
    string TeamName,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? RequestTypeId,
    string? RequestTypeName,
    bool RequiresApproval,
    string? RootCause,
    string? Resolution,
    TicketOutcome? Outcome,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    ApprovalSummary? Approval,
    bool CanWork,
    uint Version);
