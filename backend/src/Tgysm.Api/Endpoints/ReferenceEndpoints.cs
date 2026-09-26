using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Features.Reference;

namespace Tgysm.Api.Endpoints;

internal static class ReferenceEndpoints
{
    private const string Tag = "Reference";

    public static RouteGroupBuilder MapReferenceEndpoints(this RouteGroupBuilder api)
    {
        var reference = api.MapGroup(ApiRoutes.Reference).WithTags(Tag);

        reference.MapGet("/locations", async (ListReferenceLocationsHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)));
        reference.MapGet("/departments", async (ListReferenceDepartmentsHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)));
        reference.MapGet("/users", async (string? search, SearchReferenceUsersHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new SearchReferenceUsersQuery(search), ct)))
            .RequireAuthorization(Policies.ItStaffOrAdmin);

        return reference;
    }
}
