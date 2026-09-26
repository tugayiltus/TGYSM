using Tgysm.Api.Contracts;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Endpoints.Tickets;

public sealed record TransferTicketRequest(uint Version, Guid TeamId, Guid? AssigneeId, TransferReason Reason, string? Note);

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
    }
}
