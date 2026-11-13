using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Tasks;
using Tgysm.Application.Features.Work;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Endpoints.Tasks;

public sealed record AssignTaskRequest(uint Version, Guid? AssigneeId);

public sealed record TransferTaskRequest(uint Version, Guid TeamId, Guid? AssigneeId, TransferReason Reason, string? Note);

public sealed record HoldTaskRequest(uint Version, HoldReason Reason, string? Note);

public sealed record CompleteTaskRequest(uint Version, string ResultNote);

public sealed record TaskReasonRequest(uint Version, string Reason);

public sealed record TaskCommentRequest(string Body);

internal static class TaskEndpoints
{
    private const string Tag = "Tasks";
    private const string NumberRoute = "/{number}";
    private const string QueueParameter = "queue";
    private const string StatusParameter = "status";
    private const string PriorityParameter = "priority";

    public static RouteGroupBuilder MapTaskEndpoints(this RouteGroupBuilder api)
    {
        var tasks = api.MapGroup(ApiRoutes.Tasks).WithTags(Tag).RequireAuthorization();
        tasks.MapPost("", async (CreateTaskCommand command, CreateTaskHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tasks.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        tasks.MapGet(NumberRoute, async (string number, GetTaskHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetTaskQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        tasks.MapGet($"{NumberRoute}/timeline", async (string number, GetTaskTimelineHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetTaskTimelineQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        MapActions(tasks);
        api.MapGroup(ApiRoutes.WorkItems).WithTags(Tag).RequireAuthorization()
            .MapGet("", ListWorkItemsAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        return tasks;
    }

    private static async Task<Ok<PagedResult<TaskListItem>>> ListAsync(
        string? queue, string? status, string? priority, Guid? teamId, string? search, int? page, int? pageSize,
        ListTasksHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListTasksQuery(
            QueryParsing.ParseEnum<TaskQueue>(queue, QueueParameter),
            QueryParsing.ParseEnum<WorkTaskStatus>(status, StatusParameter),
            QueryParsing.ParseEnum<Priority>(priority, PriorityParameter),
            teamId,
            search,
            page ?? PageRequest.FirstPage,
            pageSize ?? PageRequest.DefaultPageSize);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }

    private static async Task<Ok<PagedResult<WorkItemRow>>> ListWorkItemsAsync(
        string? queue, int? page, int? pageSize, ListWorkItemsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListWorkItemsQuery(QueryParsing.ParseEnum<WorkQueue>(queue, QueueParameter), page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize),
            cancellationToken));

    private static void MapActions(RouteGroupBuilder tasks)
    {
        Action(tasks.MapPost($"{NumberRoute}/claim", async (string number, VersionRequest r, ClaimTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ClaimTaskCommand(number, r.Version), ct))));
        Action(tasks.MapPost($"{NumberRoute}/assign", async (string number, AssignTaskRequest r, AssignTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new AssignTaskCommand(number, r.Version, r.AssigneeId), ct))));
        Action(tasks.MapPost($"{NumberRoute}/transfer", async (string number, TransferTaskRequest r, TransferTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new TransferTaskCommand(number, r.Version, r.TeamId, r.AssigneeId, r.Reason, r.Note), ct))));
        Action(tasks.MapPost($"{NumberRoute}/start", async (string number, VersionRequest r, StartTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new StartTaskCommand(number, r.Version), ct))));
        Action(tasks.MapPost($"{NumberRoute}/hold", async (string number, HoldTaskRequest r, HoldTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new HoldTaskCommand(number, r.Version, r.Reason, r.Note), ct))));
        Action(tasks.MapPost($"{NumberRoute}/resume", async (string number, VersionRequest r, ResumeTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ResumeTaskCommand(number, r.Version), ct))));
        Action(tasks.MapPost($"{NumberRoute}/complete", async (string number, CompleteTaskRequest r, CompleteTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CompleteTaskCommand(number, r.Version, r.ResultNote), ct))));
        Action(tasks.MapPost($"{NumberRoute}/cancel", async (string number, TaskReasonRequest r, CancelTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CancelTaskCommand(number, r.Version, r.Reason), ct))));
        Action(tasks.MapPost($"{NumberRoute}/reopen", async (string number, VersionRequest r, ReopenTaskHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ReopenTaskCommand(number, r.Version), ct))));
        tasks.MapPost($"{NumberRoute}/comments", async (string number, TaskCommentRequest r, AddTaskCommentHandler h, CancellationToken ct) =>
            {
                await h.HandleAsync(new AddTaskCommentCommand(number, r.Body), ct);
                return TypedResults.NoContent();
            })
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void Action(RouteHandlerBuilder route) =>
        route.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

    private static Created<TaskDetail> Created(TaskDetail detail) =>
        TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Tasks}/{detail.Number}", detail);
}
