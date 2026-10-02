using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Knowledge;
using Tgysm.Domain.Knowledge;

namespace Tgysm.Api.Endpoints;

internal static class KnowledgeEndpoints
{
    private const string Tag = "Knowledge";
    private const string ArticlesRoute = "/articles";
    private const string IdRoute = ArticlesRoute + "/{id:guid}";
    private const string StatusParameter = "status";
    private const string VisibilityParameter = "visibility";

    public static RouteGroupBuilder MapKnowledgeEndpoints(this RouteGroupBuilder api)
    {
        var kb = api.MapGroup(ApiRoutes.Knowledge).WithTags(Tag).RequireAuthorization();

        kb.MapGet(ArticlesRoute, async (
                string? search, Guid? categoryId, string? status, string? visibility, int? page, int? pageSize, ListArticlesHandler h, CancellationToken ct) =>
            {
                var query = new ListArticlesQuery(
                    search,
                    categoryId,
                    QueryParsing.ParseEnum<ArticleStatus>(status, StatusParameter),
                    QueryParsing.ParseEnum<ArticleVisibility>(visibility, VisibilityParameter),
                    new PageRequest(page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize));
                return TypedResults.Ok(await h.HandleAsync(query, ct));
            })
            .ProducesProblem(StatusCodes.Status400BadRequest);
        kb.MapGet(IdRoute, async (Guid id, GetArticleHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(new GetArticleQuery(id), ct)))
            .ProducesProblem(StatusCodes.Status404NotFound);

        kb.MapPost(ArticlesRoute, async (CreateArticleRequest r, CreateArticleHandler h, CancellationToken ct) =>
            {
                var article = await h.HandleAsync(new CreateArticleCommand(r.Title, r.Summary, r.Body, r.CategoryId, r.Visibility), ct);
                return TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Knowledge}{ArticlesRoute}/{article.Id}", article);
            })
            .RequireAuthorization(Policies.ItStaffOrAdmin)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        kb.MapPut(IdRoute, async (Guid id, UpdateArticleRequest r, UpdateArticleHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new UpdateArticleCommand(id, r.Title, r.Summary, r.Body, r.CategoryId, r.Visibility, r.Version), ct)))
            .RequireAuthorization(Policies.ItStaffOrAdmin)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        kb.MapPost($"{IdRoute}/publish", async (Guid id, VersionRequest r, PublishArticleHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new PublishArticleCommand(id, r.Version), ct)))
            .RequireAuthorization(Policies.ItStaffOrAdmin)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        kb.MapPost($"{IdRoute}/archive", async (Guid id, VersionRequest r, ArchiveArticleHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new ArchiveArticleCommand(id, r.Version), ct)))
            .RequireAuthorization(Policies.ItStaffOrAdmin)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        kb.MapPost($"{IdRoute}/restore", async (Guid id, VersionRequest r, RestoreArticleHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new RestoreArticleCommand(id, r.Version), ct)))
            .RequireAuthorization(Policies.ItStaffOrAdmin)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        kb.MapGet($"{IdRoute}/revisions", async (Guid id, ListArticleRevisionsHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new ListArticleRevisionsQuery(id), ct)))
            .RequireAuthorization(Policies.ItStaffOrAdmin)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return kb;
    }
}
