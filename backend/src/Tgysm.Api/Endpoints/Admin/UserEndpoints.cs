using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Users;

namespace Tgysm.Api.Endpoints.Admin;

internal static class UserEndpoints
{
    private const string Tag = "Admin: Users";

    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder admin)
    {
        var users = admin.MapGroup(ApiRoutes.Users).WithTags(Tag);

        users.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest);
        users.MapGet("/{id:guid}", GetAsync).ProducesProblem(StatusCodes.Status404NotFound);
        users.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPost("/{id:guid}/deactivate", DeactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPost("/{id:guid}/reactivate", ReactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPost("/{id:guid}/reset-password", ResetPasswordAsync).ProducesProblem(StatusCodes.Status404NotFound);

        return admin;
    }

    private static async Task<Ok<PagedResult<UserListItem>>> ListAsync(
        string? search, bool? isActive, string? role, string? sort, int? page, int? pageSize,
        ListUsersHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListUsersQuery(
            search, isActive, QueryParsing.ParseRole(role), sort,
            page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }

    private static async Task<Ok<UserDetail>> GetAsync(Guid id, GetUserHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetUserQuery(id), cancellationToken));

    private static async Task<Created<TemporaryPasswordResult>> CreateAsync(
        CreateUserCommand command, CreateUserHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Users}/{result.UserId}", result);
    }

    private static async Task<Ok<UserDetail>> UpdateAsync(
        Guid id, UpdateUserRequest request, UpdateUserHandler handler, CancellationToken cancellationToken)
    {
        var command = new UpdateUserCommand(
            id, request.Version, request.FirstName, request.LastName, request.Email, request.Role, request.DepartmentId, request.LocationId);
        return TypedResults.Ok(await handler.HandleAsync(command, cancellationToken));
    }

    private static async Task<Ok<UserDetail>> DeactivateAsync(
        Guid id, VersionRequest request, SetUserActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetUserActiveCommand(id, request.Version, IsActive: false), cancellationToken));

    private static async Task<Ok<UserDetail>> ReactivateAsync(
        Guid id, VersionRequest request, SetUserActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetUserActiveCommand(id, request.Version, IsActive: true), cancellationToken));

    private static async Task<Ok<TemporaryPasswordResult>> ResetPasswordAsync(
        Guid id, ResetUserPasswordHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ResetUserPasswordCommand(id), cancellationToken));
}
