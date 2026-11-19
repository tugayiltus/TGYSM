using Tgysm.Api.Http;
using Tgysm.Application.Features.Search;

namespace Tgysm.Api.Endpoints;

internal static class SearchEndpoints
{
    private const string Tag = "Search";

    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder api)
    {
        var search = api.MapGroup(ApiRoutes.Search).WithTags(Tag).RequireAuthorization();
        search.MapGet("", async (string? q, SearchHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new SearchQuery(q), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest);
        return search;
    }
}
