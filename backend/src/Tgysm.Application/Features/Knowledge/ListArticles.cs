using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Common;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Knowledge;

/// <summary>Employees: <paramref name="Status"/> and <paramref name="Visibility"/> are ignored; they only ever see published employee articles.</summary>
public sealed record ListArticlesQuery(string? Search, Guid? CategoryId, ArticleStatus? Status, ArticleVisibility? Visibility, PageRequest Page);

public sealed class ListArticlesHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<PagedResult<ArticleListItem>> HandleAsync(ListArticlesQuery query, CancellationToken cancellationToken)
    {
        var role = await ArticleQueries.RoleOfAsync(db, actor, cancellationToken);
        var articles = db.KnowledgeArticles.AsNoTracking().Where(ArticleAccess.ReadableBy(role));

        if (role != UserRole.Employee)
        {
            if (query.Status is { } status)
            {
                articles = articles.Where(a => a.Status == status);
            }

            if (query.Visibility is { } visibility)
            {
                articles = articles.Where(a => a.Visibility == visibility);
            }
        }

        if (query.CategoryId is { } categoryId)
        {
            articles = articles.Where(a => a.CategoryId == categoryId);
        }

        var key = SearchKeyOf(query.Search);
        var ordered = key.Length == 0
            ? articles.OrderByDescending(a => a.UpdatedAt).ThenByDescending(a => a.Id)
            : articles.Where(a => a.SearchText.Contains(key))
                // SearchText starts with the folded title, so its first Title.Length characters are the title key.
                .OrderByDescending(a => a.SearchText.Substring(0, a.Title.Length).Contains(key))
                .ThenByDescending(a => a.UpdatedAt).ThenByDescending(a => a.Id);

        return await ordered
            .Select(a => new ArticleListItem(
                a.Id, a.Title, a.Summary, a.CategoryId,
                db.Categories.Where(c => c.Id == a.CategoryId).Select(c => c.Name).FirstOrDefault(),
                a.Visibility, a.Status, a.UpdatedAt))
            .ToPagedResultAsync(query.Page, cancellationToken);
    }

    /// <summary>Folded search key; empty when the input has no letter or digit (spaces and punctuation only), which means "no search".</summary>
    private static string SearchKeyOf(string? search)
    {
        var key = string.IsNullOrWhiteSpace(search) ? string.Empty : TurkishText.ToSearchKey(search);
        return key.Any(char.IsLetterOrDigit) ? key : string.Empty;
    }
}
