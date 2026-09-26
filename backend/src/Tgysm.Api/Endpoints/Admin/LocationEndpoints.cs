using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Features.Organization.Locations;

namespace Tgysm.Api.Endpoints.Admin;

internal static class LocationEndpoints
{
    private const string Tag = "Admin: Locations";

    public static RouteGroupBuilder MapLocationEndpoints(this RouteGroupBuilder admin)
    {
        var locations = admin.MapGroup(ApiRoutes.Locations).WithTags(Tag);

        locations.MapGet("", ListAsync);
        locations.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        locations.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        locations.MapPost("/{id:guid}/deactivate", DeactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        locations.MapPost("/{id:guid}/reactivate", ReactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }

    private static async Task<Ok<IReadOnlyList<LocationItem>>> ListAsync(ListLocationsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    private static async Task<Created<LocationItem>> CreateAsync(
        CreateLocationCommand command, CreateLocationHandler handler, CancellationToken cancellationToken)
    {
        var item = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Locations}/{item.Id}", item);
    }

    private static async Task<Ok<LocationItem>> UpdateAsync(
        Guid id, UpdateLocationRequest request, UpdateLocationHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new UpdateLocationCommand(id, request.Version, request.Name, request.ParentId), cancellationToken));

    private static async Task<Ok<LocationItem>> DeactivateAsync(
        Guid id, VersionRequest request, SetLocationActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetLocationActiveCommand(id, request.Version, IsActive: false), cancellationToken));

    private static async Task<Ok<LocationItem>> ReactivateAsync(
        Guid id, VersionRequest request, SetLocationActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetLocationActiveCommand(id, request.Version, IsActive: true), cancellationToken));
}
