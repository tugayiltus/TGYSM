using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Organization.Teams;

namespace Tgysm.Api.Endpoints.Admin;

internal static class TeamEndpoints
{
    private const string Tag = "Admin: Teams";

    public static RouteGroupBuilder MapTeamEndpoints(this RouteGroupBuilder admin)
    {
        var teams = admin.MapGroup(ApiRoutes.Teams).WithTags(Tag);

        teams.MapGet("", ListAsync);
        teams.MapGet("/{id:guid}", GetAsync).ProducesProblem(StatusCodes.Status404NotFound);
        teams.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapPost("/{id:guid}/deactivate", DeactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapPost("/{id:guid}/reactivate", ReactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapPost("/{id:guid}/members", AddMemberAsync).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        teams.MapDelete("/{id:guid}/members/{userId:guid}", RemoveMemberAsync).ProducesProblem(StatusCodes.Status404NotFound);

        return admin;
    }

    private static async Task<Ok<PagedResult<TeamItem>>> ListAsync(
        string? search, bool? isActive, int? page, int? pageSize, ListTeamsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListTeamsQuery(search, isActive, page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize), cancellationToken));

    private static async Task<Ok<TeamDetail>> GetAsync(Guid id, GetTeamHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetTeamQuery(id), cancellationToken));

    private static async Task<Created<TeamDetail>> CreateAsync(CreateTeamCommand command, CreateTeamHandler handler, CancellationToken cancellationToken)
    {
        var detail = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Teams}/{detail.Id}", detail);
    }

    private static async Task<Ok<TeamDetail>> UpdateAsync(Guid id, UpdateTeamRequest request, UpdateTeamHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new UpdateTeamCommand(id, request.Version, request.Name), cancellationToken));

    private static async Task<Ok<TeamDetail>> DeactivateAsync(Guid id, VersionRequest request, SetTeamActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetTeamActiveCommand(id, request.Version, IsActive: false), cancellationToken));

    private static async Task<Ok<TeamDetail>> ReactivateAsync(Guid id, VersionRequest request, SetTeamActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetTeamActiveCommand(id, request.Version, IsActive: true), cancellationToken));

    private static async Task<Ok<TeamDetail>> AddMemberAsync(Guid id, AddTeamMemberRequest request, AddTeamMemberHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new AddTeamMemberCommand(id, request.UserId), cancellationToken));

    private static async Task<Ok<TeamDetail>> RemoveMemberAsync(Guid id, Guid userId, RemoveTeamMemberHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new RemoveTeamMemberCommand(id, userId), cancellationToken));
}
