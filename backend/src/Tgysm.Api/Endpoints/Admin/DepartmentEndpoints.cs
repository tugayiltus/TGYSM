using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Organization.Departments;

namespace Tgysm.Api.Endpoints.Admin;

internal static class DepartmentEndpoints
{
    private const string Tag = "Admin: Departments";

    public static RouteGroupBuilder MapDepartmentEndpoints(this RouteGroupBuilder admin)
    {
        var departments = admin.MapGroup(ApiRoutes.Departments).WithTags(Tag);

        departments.MapGet("", ListAsync);
        departments.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        departments.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        departments.MapPost("/{id:guid}/deactivate", DeactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);
        departments.MapPost("/{id:guid}/reactivate", ReactivateAsync).ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }

    private static async Task<Ok<PagedResult<DepartmentItem>>> ListAsync(
        string? search, bool? isActive, int? page, int? pageSize, ListDepartmentsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListDepartmentsQuery(search, isActive, page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize),
            cancellationToken));

    private static async Task<Created<DepartmentItem>> CreateAsync(
        CreateDepartmentCommand command, CreateDepartmentHandler handler, CancellationToken cancellationToken)
    {
        var item = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Departments}/{item.Id}", item);
    }

    private static async Task<Ok<DepartmentItem>> UpdateAsync(
        Guid id, UpdateDepartmentRequest request, UpdateDepartmentHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new UpdateDepartmentCommand(id, request.Version, request.Name, request.ApproverUserId), cancellationToken));

    private static async Task<Ok<DepartmentItem>> DeactivateAsync(
        Guid id, VersionRequest request, SetDepartmentActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetDepartmentActiveCommand(id, request.Version, IsActive: false), cancellationToken));

    private static async Task<Ok<DepartmentItem>> ReactivateAsync(
        Guid id, VersionRequest request, SetDepartmentActiveHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetDepartmentActiveCommand(id, request.Version, IsActive: true), cancellationToken));
}
