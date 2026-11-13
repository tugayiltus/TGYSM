using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Changes;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Endpoints.Changes;

public sealed record EditChangeRequest(uint Version, ChangeDraft Draft, IReadOnlyList<Guid> AssetIds);

internal static class ChangeEndpoints
{
    private const string Tag = "Changes";
    private const string NumberRoute = "/{number}";
    private const string QueueParameter = "queue";
    private const string StatusParameter = "status";
    private const string RiskParameter = "risk";

    public static RouteGroupBuilder MapChangeEndpoints(this RouteGroupBuilder api)
    {
        var changes = api.MapGroup(ApiRoutes.Changes).WithTags(Tag).RequireAuthorization();
        changes.MapPost("", async (CreateChangeCommand command, CreateChangeHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        changes.MapGet(NumberRoute, async (string number, GetChangeHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetChangeQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        changes.MapPut(NumberRoute, async (string number, EditChangeRequest r, EditChangeHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new EditChangeCommand(number, r.Version, r.Draft, r.AssetIds), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapGet($"{NumberRoute}/timeline", async (string number, GetChangeTimelineHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetChangeTimelineQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        return changes;
    }

    private static async Task<Ok<PagedResult<ChangeListItem>>> ListAsync(
        string? queue, string? status, string? risk, string? search, int? page, int? pageSize,
        ListChangesHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListChangesQuery(
            QueryParsing.ParseEnum<ChangeQueue>(queue, QueueParameter),
            QueryParsing.ParseEnum<ChangeStatus>(status, StatusParameter),
            QueryParsing.ParseEnum<Level>(risk, RiskParameter),
            search,
            page ?? PageRequest.FirstPage,
            pageSize ?? PageRequest.DefaultPageSize);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }

    private static Created<ChangeDetail> Created(ChangeDetail detail) =>
        TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Changes}/{detail.Number}", detail);
}
