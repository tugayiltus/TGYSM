using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Assets;
using Tgysm.Application.Features.Assets.Catalog;
using Tgysm.Application.Features.Assets.Lifecycle;
using Tgysm.Api.Contracts;
using Tgysm.Domain.Assets;

namespace Tgysm.Api.Endpoints.Assets;

public sealed record AssignAssetRequest(uint Version, Guid UserId, Guid? RequestTicketId, string? Note);

public sealed record ReturnAssetRequest(uint Version, Guid LocationId);

public sealed record TransferAssetRequest(uint Version, Guid UserId, string? Note);

public sealed record StartMaintenanceRequest(uint Version, bool RetainResponsibility);

public sealed record AssetReasonRequest(uint Version, string Reason);

public sealed record AssetLocationRequest(uint Version, Guid LocationId);

public sealed record UpdateAssetRequest(
    uint Version,
    string AssetTag,
    Guid ModelId,
    string? SerialNumber,
    Guid LocationId,
    Guid? DepartmentId,
    DateOnly? WarrantyEndsOn,
    DateOnly? PurchasedOn,
    string? Notes);

internal static class AssetEndpoints
{
    private const string Tag = "Assets";
    private const string TypeParameter = "type";
    private const string StatusParameter = "status";

    public static RouteGroupBuilder MapAssetEndpoints(this RouteGroupBuilder api)
    {
        var assets = api.MapGroup(ApiRoutes.Assets).WithTags(Tag).RequireAuthorization(Policies.ItStaffOrAdmin);

        assets.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest);
        // Under /assets/catalog: static segment, so it never matches the {id:guid} routes.
        assets.MapGet("/catalog", async (GetAssetCatalogHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        assets.MapGet("/{id:guid}", GetAsync).ProducesProblem(StatusCodes.Status404NotFound);
        assets.MapGet("/{id:guid}/history", GetHistoryAsync).ProducesProblem(StatusCodes.Status404NotFound);
        assets.MapGet("/{id:guid}/tickets", async (Guid id, Application.Features.Tickets.ListAssetTicketsHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new Application.Features.Tickets.ListAssetTicketsQuery(id), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        assets.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        assets.MapPut("/{id:guid}", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);

        assets.MapPost("/{id:guid}/assign", (Guid id, AssignAssetRequest r, AssetLifecycleHandler h, CancellationToken ct) =>
            Ok(h.AssignAsync(new AssignAssetCommand(id, r.Version, r.UserId, r.RequestTicketId, r.Note), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        assets.MapPost("/{id:guid}/return", (Guid id, ReturnAssetRequest r, AssetLifecycleHandler h, CancellationToken ct) =>
            Ok(h.ReturnAsync(new ReturnAssetCommand(id, r.Version, r.LocationId), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        assets.MapPost("/{id:guid}/transfer", (Guid id, TransferAssetRequest r, AssetLifecycleHandler h, CancellationToken ct) =>
            Ok(h.TransferAsync(new TransferAssetCommand(id, r.Version, r.UserId, r.Note), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        assets.MapPost("/{id:guid}/maintenance/start", (Guid id, StartMaintenanceRequest r, AssetLifecycleHandler h, CancellationToken ct) =>
            Ok(h.StartMaintenanceAsync(new StartMaintenanceCommand(id, r.Version, r.RetainResponsibility), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        assets.MapPost("/{id:guid}/maintenance/end", (Guid id, VersionRequest r, AssetLifecycleHandler h, CancellationToken ct) =>
            Ok(h.EndMaintenanceAsync(new EndMaintenanceCommand(id, r.Version), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        assets.MapPost("/{id:guid}/lost", (Guid id, AssetReasonRequest r, AssetLifecycleHandler h, CancellationToken ct) =>
            Ok(h.MarkLostAsync(new MarkAssetLostCommand(id, r.Version, r.Reason), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        assets.MapPost("/{id:guid}/found", (Guid id, AssetLocationRequest r, AssetLifecycleHandler h, CancellationToken ct) =>
            Ok(h.MarkFoundAsync(new MarkAssetFoundCommand(id, r.Version, r.LocationId), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        assets.MapPost("/{id:guid}/retire", (Guid id, AssetReasonRequest r, AssetLifecycleHandler h, CancellationToken ct) =>
            Ok(h.RetireAsync(new RetireAssetCommand(id, r.Version, r.Reason), ct))).ProducesProblem(StatusCodes.Status409Conflict);

        return assets;
    }

    public static RouteGroupBuilder MapMyAssetEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(ApiRoutes.MyAssets, async (ListMyAssetsHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithTags(Tag);
        return api;
    }

    private static async Task<Ok<AssetItem>> Ok(Task<AssetItem> operation) => TypedResults.Ok(await operation);

    private static async Task<Ok<PagedResult<AssetItem>>> ListAsync(
        string? search, string? type, string? status, Guid? locationId, int? page, int? pageSize,
        ListAssetsHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListAssetsQuery(
            search,
            QueryParsing.ParseEnum<AssetType>(type, TypeParameter),
            QueryParsing.ParseEnum<AssetStatus>(status, StatusParameter),
            locationId,
            page ?? PageRequest.FirstPage,
            pageSize ?? PageRequest.DefaultPageSize);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }

    private static async Task<Ok<AssetItem>> GetAsync(Guid id, GetAssetHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetAssetQuery(id), cancellationToken));

    private static async Task<Ok<AssetHistory>> GetHistoryAsync(Guid id, GetAssetHistoryHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new GetAssetHistoryQuery(id), cancellationToken));

    private static async Task<Created<AssetItem>> CreateAsync(CreateAssetCommand command, CreateAssetHandler handler, CancellationToken cancellationToken)
    {
        var item = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Assets}/{item.Id}", item);
    }

    private static async Task<Ok<AssetItem>> UpdateAsync(Guid id, UpdateAssetRequest request, UpdateAssetHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new UpdateAssetCommand(id, request.Version, request.AssetTag, request.ModelId, request.SerialNumber,
                request.LocationId, request.DepartmentId, request.WarrantyEndsOn, request.PurchasedOn, request.Notes),
            cancellationToken));
}
