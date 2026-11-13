using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Changes;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Endpoints.Changes;

public sealed record EditChangeRequest(uint Version, ChangeDraft Draft, IReadOnlyList<Guid> AssetIds);

public sealed record ChangeVersionRequest(uint Version);

public sealed record DecideChangeApprovalRequest(uint ApprovalVersion, string? Reason);

public sealed record CloseChangeRequest(
    uint Version, ChangeOutcome Outcome, string? VerificationNote, string? BackoutNote, string? ClosingNote, DateTimeOffset? ActualEnd);

public sealed record CancelChangeRequest(uint Version, string Reason);

internal static class ChangeEndpoints
{
    private const string Tag = "Changes";
    private const string NumberRoute = "/{number}";
    private const string QueueParameter = "queue";
    private const string StatusParameter = "status";
    private const string RiskParameter = "risk";

    public static RouteGroupBuilder MapChangeEndpoints(this RouteGroupBuilder api)
    {
        var changes = api.MapGroup(ApiRoutes.Changes).WithTags(Tag).RequireAuthorization();
        changes.MapPost("", async (CreateChangeCommand command, CreateChangeHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        changes.MapGet(NumberRoute, async (string number, GetChangeHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetChangeQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        changes.MapPut(NumberRoute, async (string number, EditChangeRequest r, EditChangeHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new EditChangeCommand(number, r.Version, r.Draft, r.AssetIds), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapGet($"{NumberRoute}/timeline", async (string number, GetChangeTimelineHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetChangeTimelineQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        changes.MapPost($"{NumberRoute}/submit", async (string number, ChangeVersionRequest r, SubmitChangeHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new SubmitChangeCommand(number, r.Version), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapPost($"{NumberRoute}/withdraw", async (string number, ChangeVersionRequest r, WithdrawChangeHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new WithdrawChangeCommand(number, r.Version), ct)))
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapPost($"{NumberRoute}/start-implementation", async (string number, ChangeVersionRequest r, StartImplementationHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new StartImplementationCommand(number, r.Version), ct)))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapPost($"{NumberRoute}/finish-implementation", async (string number, ChangeVersionRequest r, FinishImplementationHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new FinishImplementationCommand(number, r.Version), ct)))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapPost($"{NumberRoute}/start-backout", async (string number, ChangeVersionRequest r, StartBackoutHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new StartBackoutCommand(number, r.Version), ct)))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapPost($"{NumberRoute}/close", async (string number, CloseChangeRequest r, CloseChangeHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(
                    new CloseChangeCommand(number, r.Version, r.Outcome, r.VerificationNote, r.BackoutNote, r.ClosingNote, r.ActualEnd), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapPost($"{NumberRoute}/cancel", async (string number, CancelChangeRequest r, CancelChangeHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new CancelChangeCommand(number, r.Version, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        MapDecision(changes, "approve", ChangeApprovalDecision.Approve);
        MapDecision(changes, "reject", ChangeApprovalDecision.Reject);
        MapDecision(changes, "return", ChangeApprovalDecision.Return);

        api.MapGroup(ApiRoutes.ChangeApprovals).WithTags(Tag).RequireAuthorization()
            .MapGet("", async (ListChangeApprovalsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new ListChangeApprovalsQuery(), ct)));
        return changes;
    }

    private static void MapDecision(RouteGroupBuilder changes, string action, ChangeApprovalDecision decision) =>
        changes.MapPost($"{NumberRoute}/approval/{action}", async (string number, DecideChangeApprovalRequest r, DecideChangeApprovalHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new DecideChangeApprovalCommand(number, decision, r.ApprovalVersion, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Ok<PagedResult<ChangeListItem>>> ListAsync(
        string? queue, string? status, string? risk, string? search, int? page, int? pageSize,
        ListChangesHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListChangesQuery(
            QueryParsing.ParseEnum<ChangeQueue>(queue, QueueParameter),
            QueryParsing.ParseEnum<ChangeStatus>(status, StatusParameter),
            QueryParsing.ParseEnum<Level>(risk, RiskParameter),
            search,
            page ?? PageRequest.FirstPage,
            pageSize ?? PageRequest.DefaultPageSize);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }

    private static Created<ChangeDetail> Created(ChangeDetail detail) =>
        TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Changes}/{detail.Number}", detail);
}
