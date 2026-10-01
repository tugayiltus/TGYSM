using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Features.Operations;

namespace Tgysm.Api.Endpoints;

internal sealed record HealthResponse(string Status);

internal static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(ApiRoutes.Health, GetAsync).AllowAnonymous().WithTags("Health");
        return api;
    }

    private static async Task<Ok<HealthResponse>> GetAsync(ListWorkerHeartbeatsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(new HealthResponse((await handler.HandleAsync(cancellationToken)).Status));
}
