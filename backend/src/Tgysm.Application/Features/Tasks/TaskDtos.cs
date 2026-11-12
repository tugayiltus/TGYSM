using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tasks;

/// <summary>
/// The ticket a task is linked to. Outside the viewer's ticket scope only <see cref="Accessible"/> = false is
/// shown, never the number or title (ADR-0031).
/// </summary>
public sealed record LinkedTicket(bool Accessible, string? Number, TicketKind? Kind, string? Title, TicketStatus? Status);

public sealed record TaskDetail(
    Guid Id,
    string Number,
    string Title,
    string? Description,
    WorkTaskStatus Status,
    HoldReason? HoldReason,
    string? HoldNote,
    Priority Priority,
    DateOnly? DueDate,
    bool IsOverdue,
    Guid TeamId,
    string TeamName,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid CreatedById,
    string CreatedByName,
    LinkedTicket? Link,
    string? ResultNote,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    bool CanWork,
    uint Version);
