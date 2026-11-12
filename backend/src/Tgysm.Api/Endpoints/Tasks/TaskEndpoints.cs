using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Features.Tasks;

namespace Tgysm.Api.Endpoints.Tasks;

internal static class TaskEndpoints
{
    private const string Tag = "Tasks";

    public static RouteGroupBuilder MapTaskEndpoints(this RouteGroupBuilder api)
    {
        var tasks = api.MapGroup(ApiRoutes.Tasks).WithTags(Tag).RequireAuthorization();
        tasks.MapPost("", async (CreateTaskCommand command, CreateTaskHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tasks.MapGet("/{number}", async (string number, GetTaskHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetTaskQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        return tasks;
    }

    private static Created<TaskDetail> Created(TaskDetail detail) =>
        TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Tasks}/{detail.Number}", detail);
}
