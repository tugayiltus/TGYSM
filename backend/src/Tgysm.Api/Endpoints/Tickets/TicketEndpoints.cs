using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Endpoints.Tickets;

internal static partial class TicketEndpoints
{
    private const string Tag = "Tickets";
    private const string QueueParameter = "queue";
    private const string StatusParameter = "status";
    private const string KindParameter = "kind";
    private const string PriorityParameter = "priority";

    public static RouteGroupBuilder MapTicketEndpoints(this RouteGroupBuilder api)
    {
        var tickets = api.MapGroup(ApiRoutes.Tickets).WithTags(Tag).RequireAuthorization();

        tickets.MapPost("/incidents", async (CreateIncidentCommand command, CreateIncidentHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost("/requests", async (CreateRequestCommand command, CreateRequestHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest);
        tickets.MapGet("/{number}", async (string number, GetTicketHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetTicketQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);

        tickets.MapTicketActionEndpoints();
        return tickets;
    }

    private static Created<TicketDetail> Created(TicketDetail detail) =>
        TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Tickets}/{detail.Number}", detail);

    private static async Task<Ok<PagedResult<TicketListItem>>> ListAsync(
        string? queue, string? status, string? kind, string? priority, string? search, int? page, int? pageSize,
        ListTicketsHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListTicketsQuery(
            QueryParsing.ParseEnum<TicketQueue>(queue, QueueParameter),
            QueryParsing.ParseEnum<TicketStatus>(status, StatusParameter),
            QueryParsing.ParseEnum<TicketKind>(kind, KindParameter),
            QueryParsing.ParseEnum<Priority>(priority, PriorityParameter),
            search,
            page ?? PageRequest.FirstPage,
            pageSize ?? PageRequest.DefaultPageSize);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }
}
