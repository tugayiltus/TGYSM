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

public sealed record AddChangeTicketRequest(string TicketNumber, ChangeTicketLinkKind Kind);

internal static class ChangeEndpoints
{
    private const string Tag = "Changes";
    private const string NumberRoute = "/{number}";
    private const string CalendarRoute = "/calendar";
    private const string DateFormat = "yyyy-MM-dd";
    private const string QueueParameter = "queue";
    private const string StatusParameter = "status";
    private const string RiskParameter = "risk";
    private const string ApproveSegment = "approve";
    private const string RejectSegment = "reject";
    private const string ReturnSegment = "return";
    private const string TicketsSegment = "tickets";
    private const string TicketNumberRoute = "{ticketNumber}";

    public static RouteGroupBuilder MapChangeEndpoints(this RouteGroupBuilder api)
    {
        var changes = api.MapGroup(ApiRoutes.Changes).WithTags(Tag).RequireAuthorization();
        changes.MapPost("", async (CreateChangeCommand command, CreateChangeHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapGet(CalendarRoute, CalendarAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
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
        changes.MapGet($"{NumberRoute}/{TicketsSegment}", async (string number, ListChangeTicketsHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new ListChangeTicketsQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        changes.MapPost($"{NumberRoute}/{TicketsSegment}", async (string number, AddChangeTicketRequest r, AddChangeTicketHandler handler, CancellationToken ct) =>
                TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Changes}/{number}/{TicketsSegment}", await handler.HandleAsync(new AddChangeTicketCommand(number, r.TicketNumber, r.Kind), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        changes.MapDelete($"{NumberRoute}/{TicketsSegment}/{TicketNumberRoute}", async (string number, string ticketNumber, RemoveChangeTicketHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new RemoveChangeTicketCommand(number, ticketNumber), ct)))
            .ProducesProblem(StatusCodes.Status404NotFound);
        MapDecision(changes, ApproveSegment, ChangeApprovalDecision.Approve);
        MapDecision(changes, RejectSegment, ChangeApprovalDecision.Reject);
        MapDecision(changes, ReturnSegment, ChangeApprovalDecision.Return);

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

    private static async Task<Ok<IReadOnlyList<ChangeCalendarItem>>> CalendarAsync(
        string? from, string? to, GetChangeCalendarHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetChangeCalendarQuery(ParseDay(from), ParseDay(to)), cancellationToken));

    /// <summary>A missing or malformed day is null; the handler answers it with the calendar range error.</summary>
    private static DateOnly? ParseDay(string? value) =>
        DateOnly.TryParseExact(value, DateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day) ? day : null;

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
