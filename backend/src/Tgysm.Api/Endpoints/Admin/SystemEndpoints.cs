using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Features.Operations;

namespace Tgysm.Api.Endpoints.Admin;

internal static class SystemEndpoints
{
    private const string Tag = "Admin: System";

    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGroup(ApiRoutes.System).WithTags(Tag).MapGet("/workers", ListWorkersAsync);
        return admin;
    }

    private static async Task<Ok<WorkerHeartbeatsResult>> ListWorkersAsync(ListWorkerHeartbeatsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
