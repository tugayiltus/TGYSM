using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Features.Approvals;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Endpoints.Tickets;

public sealed record TransferTicketRequest(uint Version, Guid TeamId, Guid? AssigneeId, TransferReason Reason, string? Note);

public sealed record HoldTicketRequest(uint Version, HoldReason? Reason, string? Note);

public sealed record CompleteTicketRequest(uint Version, string Resolution, TicketOutcome? Outcome, string? RootCause);

public sealed record TicketReasonRequest(uint Version, string Reason);

public sealed record ChangePriorityRequest(uint Version, Level? Impact, Level? Urgency, string Reason);

public sealed record ReassignApproverRequest(Guid ApproverId, string Reason);

public sealed record RelatedRequestRequest(Guid RequestTypeId);

public sealed record LinkAssetRequest(Guid AssetId);

internal static partial class TicketEndpoints
{
    private static async Task<Created<CommentItem>> AddCommentAsync(string number, HttpRequest request, AddCommentHandler handler, CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var visibility = QueryParsing.ParseEnum<Visibility>(form[VisibilityField].FirstOrDefault(), VisibilityField);
        var files = form.Files.GetFiles(AddCommentHandler.FilesField)
            .Select(f => new UploadedFile(f.FileName, f.Length, f.OpenReadStream)).ToList();
        var item = await handler.HandleAsync(new AddCommentCommand(number, form[BodyField].FirstOrDefault(), visibility, files), cancellationToken);
        return TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Tickets}/{number}/comments/{item.Id}", item);
    }

    private const string NumberRoute = "/{number}";
    private const string BodyField = "body";
    private const string VisibilityField = "visibility";
    private const string NoSniffHeader = "X-Content-Type-Options";
    private const string NoSniff = "nosniff";

    /// <summary>Five files of 10 MB plus multipart overhead; larger bodies are cut before model binding.</summary>
    private const long CommentRequestLimitBytes = (AttachmentTypes.MaxFilesPerUpload * AttachmentTypes.MaxBytes) + (1024 * 1024);

    /// <summary>Ticket workflow actions. Every body carries the ticket's version (optimistic concurrency).</summary>
    private static void MapTicketActionEndpoints(this RouteGroupBuilder tickets)
    {
        // Antiforgery is checked for every unsafe /api request by CsrfValidationMiddleware (header token), not by form binding.
        tickets.MapPost($"{NumberRoute}/comments", AddCommentAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(CommentRequestLimitBytes))
            .Accepts<IFormCollection>("multipart/form-data")
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapGet($"{NumberRoute}/comments", async (string number, ListCommentsHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ListCommentsQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        tickets.MapGet($"{NumberRoute}/timeline", async (string number, GetTimelineHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new GetTimelineQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        tickets.MapGet($"{NumberRoute}/attachments/{{id:guid}}", async (string number, Guid id, DownloadAttachmentHandler h, HttpContext http, CancellationToken ct) =>
        {
            var file = await h.HandleAsync(new DownloadAttachmentQuery(number, id), ct);
            http.Response.Headers[NoSniffHeader] = NoSniff;
            return TypedResults.File(file.Content, file.ContentType, file.FileName);
        }).ProducesProblem(StatusCodes.Status404NotFound);

        tickets.MapPost($"{NumberRoute}/claim", async (string number, VersionRequest r, ClaimTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ClaimTicketCommand(number, r.Version), ct))).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/transfer", async (string number, TransferTicketRequest r, TransferTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new TransferTicketCommand(number, r.Version, r.TeamId, r.AssigneeId, r.Reason, r.Note), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/start", async (string number, VersionRequest r, StartTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new StartTicketCommand(number, r.Version), ct))).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/hold", async (string number, HoldTicketRequest r, HoldTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new HoldTicketCommand(number, r.Version, r.Reason, r.Note), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/resume", async (string number, VersionRequest r, ResumeTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ResumeTicketCommand(number, r.Version), ct))).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/complete", async (string number, CompleteTicketRequest r, CompleteTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CompleteTicketCommand(number, r.Version, r.Resolution, r.Outcome, r.RootCause), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/cancel", async (string number, TicketReasonRequest r, CancelTicketHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CancelTicketCommand(number, r.Version, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/priority", async (string number, ChangePriorityRequest r, ChangePriorityHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ChangePriorityCommand(number, r.Version, r.Impact, r.Urgency, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/related-request", async (string number, RelatedRequestRequest r, CreateRelatedRequestHandler h, CancellationToken ct) =>
            {
                var request = await h.HandleAsync(new CreateRelatedRequestCommand(number, r.RequestTypeId), ct);
                return TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Tickets}/{request.Number}", request);
            })
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapGet($"{NumberRoute}/assets", async (string number, ListTicketAssetsHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ListTicketAssetsQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        tickets.MapPost($"{NumberRoute}/assets", async (string number, LinkAssetRequest r, LinkTicketAssetHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new LinkTicketAssetCommand(number, r.AssetId), ct))).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapDelete($"{NumberRoute}/assets/{{assetId:guid}}", async (string number, Guid assetId, UnlinkTicketAssetHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new UnlinkTicketAssetCommand(number, assetId), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        tickets.MapPost($"{NumberRoute}/approval/reassign", async (string number, ReassignApproverRequest r, ReassignApproverHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ReassignApproverCommand(number, r.ApproverId, r.Reason), ct)))
            .RequireAuthorization(Policies.Admin)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost($"{NumberRoute}/approval/invalidate", async (string number, TicketReasonRequest r, InvalidateApprovalHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new InvalidateApprovalCommand(number, r.Version, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }
}
