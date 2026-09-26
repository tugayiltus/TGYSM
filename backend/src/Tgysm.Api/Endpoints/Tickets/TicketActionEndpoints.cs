using Tgysm.Api.Contracts;
using Tgysm.Api.Security;
using Tgysm.Application.Features.Approvals;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Endpoints.Tickets;

public sealed record TransferTicketRequest(uint Version, Guid TeamId, Guid? AssigneeId, TransferReason Reason, string? Note);

public sealed record HoldTicketRequest(uint Version, HoldReason? Reason, string? Note);

public sealed record CompleteTicketRequest(uint Version, string Resolution, TicketOutcome? Outcome, string? RootCause);

public sealed record TicketReasonRequest(uint Version, string Reason);

public sealed record ChangePriorityRequest(uint Version, Level? Impact, Level? Urgency, string Reason);

public sealed record ReassignApproverRequest(Guid ApproverId, string Reason);

internal static partial class TicketEndpoints
{
    private const string NumberRoute = "/{number}";

    /// <summary>Ticket workflow actions. Every body carries the ticket's version (optimistic concurrency).</summary>
    private static void MapTicketActionEndpoints(this RouteGroupBuilder tickets)
    {
        tickets.MapPost($"{NumberRoute}/claim", async (string number, VersionRequest r, ClaimTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ClaimTicketCommand(number, r.Version), ct))).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/transfer", async (string number, TransferTicketRequest r, TransferTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new TransferTicketCommand(number, r.Version, r.TeamId, r.AssigneeId, r.Reason, r.Note), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/start", async (string number, VersionRequest r, StartTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new StartTicketCommand(number, r.Version), ct))).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/hold", async (string number, HoldTicketRequest r, HoldTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new HoldTicketCommand(number, r.Version, r.Reason, r.Note), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/resume", async (string number, VersionRequest r, ResumeTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ResumeTicketCommand(number, r.Version), ct))).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/complete", async (string number, CompleteTicketRequest r, CompleteTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CompleteTicketCommand(number, r.Version, r.Resolution, r.Outcome, r.RootCause), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/cancel", async (string number, TicketReasonRequest r, CancelTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CancelTicketCommand(number, r.Version, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/priority", async (string number, ChangePriorityRequest r, ChangePriorityHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ChangePriorityCommand(number, r.Version, r.Impact, r.Urgency, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/approval/reassign", async (string number, ReassignApproverRequest r, ReassignApproverHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ReassignApproverCommand(number, r.ApproverId, r.Reason), ct)))
            .RequireAuthorization(Policies.Admin)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/approval/invalidate", async (string number, TicketReasonRequest r, InvalidateApprovalHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new InvalidateApprovalCommand(number, r.Version, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }
}
