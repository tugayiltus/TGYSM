using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Features.Assets.Catalog;
using Tgysm.Application.Features.Catalog;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Catalog;

namespace Tgysm.Api.Endpoints.Admin;

public sealed record UpdateCategoryRequest(uint Version, string Name, Guid? ParentId, CategoryScope Scope);

public sealed record UpdateRequestTypeRequest(uint Version, string Name, bool RequiresApproval);

public sealed record RenameCatalogEntryRequest(uint Version, string Name);

internal static class CatalogEndpoints
{
    private const string CategoriesTag = "Admin: Categories";
    private const string RequestTypesTag = "Admin: Request types";
    private const string DeviceCatalogTag = "Admin: Device catalog";
    private const string TypeParameter = "type";

    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder admin)
    {
        var categories = admin.MapGroup(ApiRoutes.Categories).WithTags(CategoriesTag);
        categories.MapGet("", async (ListCategoriesHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        categories.MapPost("", async (CreateCategoryCommand command, CreateCategoryHandler h, CancellationToken ct) =>
        {
            var item = await h.HandleAsync(command, ct);
            return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Categories}/{item.Id}", item);
        }).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        categories.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest r, UpdateCategoryHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new UpdateCategoryCommand(id, r.Version, r.Name, r.ParentId, r.Scope), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        categories.MapPost("/{id:guid}/deactivate", async (Guid id, VersionRequest r, SetCategoryActiveHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetCategoryActiveCommand(id, r.Version, IsActive: false), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        categories.MapPost("/{id:guid}/reactivate", async (Guid id, VersionRequest r, SetCategoryActiveHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetCategoryActiveCommand(id, r.Version, IsActive: true), ct))).ProducesProblem(StatusCodes.Status409Conflict);

        var types = admin.MapGroup(ApiRoutes.RequestTypes).WithTags(RequestTypesTag);
        types.MapGet("", async (ListRequestTypesHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        types.MapPost("", async (CreateRequestTypeCommand command, CreateRequestTypeHandler h, CancellationToken ct) =>
        {
            var item = await h.HandleAsync(command, ct);
            return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.RequestTypes}/{item.Id}", item);
        }).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        types.MapPut("/{id:guid}", async (Guid id, UpdateRequestTypeRequest r, UpdateRequestTypeHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new UpdateRequestTypeCommand(id, r.Version, r.Name, r.RequiresApproval), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        types.MapPost("/{id:guid}/deactivate", async (Guid id, VersionRequest r, SetRequestTypeActiveHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetRequestTypeActiveCommand(id, r.Version, IsActive: false), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        types.MapPost("/{id:guid}/reactivate", async (Guid id, VersionRequest r, SetRequestTypeActiveHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetRequestTypeActiveCommand(id, r.Version, IsActive: true), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        var brands = admin.MapGroup(ApiRoutes.Brands).WithTags(DeviceCatalogTag);
        brands.MapGet("", async (ListBrandsHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        brands.MapPost("", async (CreateBrandCommand command, CreateBrandHandler h, CancellationToken ct) =>
        {
            var item = await h.HandleAsync(command, ct);
            return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Brands}/{item.Id}", item);
        }).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        brands.MapPut("/{id:guid}", async (Guid id, RenameCatalogEntryRequest r, RenameBrandHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new RenameBrandCommand(id, r.Version, r.Name), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        brands.MapPost("/{id:guid}/deactivate", async (Guid id, VersionRequest r, SetBrandActiveHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetBrandActiveCommand(id, r.Version, IsActive: false), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        brands.MapPost("/{id:guid}/reactivate", async (Guid id, VersionRequest r, SetBrandActiveHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetBrandActiveCommand(id, r.Version, IsActive: true), ct))).ProducesProblem(StatusCodes.Status409Conflict);

        var models = admin.MapGroup(ApiRoutes.AssetModels).WithTags(DeviceCatalogTag);
        models.MapGet("", async (Guid? brandId, string? type, ListAssetModelsHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new ListAssetModelsQuery(brandId, QueryParsing.ParseEnum<AssetType>(type, TypeParameter)), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest);
        models.MapPost("", async (CreateAssetModelCommand command, CreateAssetModelHandler h, CancellationToken ct) =>
        {
            var item = await h.HandleAsync(command, ct);
            return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.AssetModels}/{item.Id}", item);
        }).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        models.MapPut("/{id:guid}", async (Guid id, RenameCatalogEntryRequest r, RenameAssetModelHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new RenameAssetModelCommand(id, r.Version, r.Name), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        models.MapPost("/{id:guid}/deactivate", async (Guid id, VersionRequest r, SetAssetModelActiveHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetAssetModelActiveCommand(id, r.Version, IsActive: false), ct))).ProducesProblem(StatusCodes.Status409Conflict);
        models.MapPost("/{id:guid}/reactivate", async (Guid id, VersionRequest r, SetAssetModelActiveHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetAssetModelActiveCommand(id, r.Version, IsActive: true), ct))).ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }
}
