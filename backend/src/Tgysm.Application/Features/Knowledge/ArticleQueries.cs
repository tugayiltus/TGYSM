using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Knowledge;

internal static class ArticleQueries
{
    private const string NameSeparator = " ";
    private const string CategoryField = "categoryId";
    private const string InactiveCategoryMessage = "The category does not exist or is inactive.";

    /// <summary>The signed-in user's role; article visibility depends on it (spec §6.2).</summary>
    public static async Task<UserRole> RoleOfAsync(IAppDbContext db, ICurrentActor actor, CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync(cancellationToken);
    }

    /// <summary>Name of an existing active category, or a field error on <c>categoryId</c>.</summary>
    public static async Task<string?> ActiveCategoryNameAsync(IAppDbContext db, Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is not { } id)
        {
            return null;
        }

        return await db.Categories.AsNoTracking().Where(c => c.Id == id && c.IsActive).Select(c => c.Name).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ValidationException([new ValidationFailure(CategoryField, InactiveCategoryMessage) { ErrorCode = ErrorCodes.ReferenceInactive }]);
    }

    public static Task<ArticleDetail> LoadDetailAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.KnowledgeArticles.AsNoTracking().Where(a => a.Id == id)
            .Select(a => new ArticleDetail(
                a.Id, a.Title, a.Summary, a.Body, a.CategoryId,
                db.Categories.Where(c => c.Id == a.CategoryId).Select(c => c.Name).FirstOrDefault(),
                a.Visibility, a.Status,
                db.Users.Where(u => u.Id == a.AuthorId).Select(u => u.FirstName + NameSeparator + u.LastName).First(),
                a.PublishedAt, a.UpdatedAt, a.Version))
            .SingleAsync(cancellationToken);

    /// <summary>Tracked article for a write, with the expected version set; unknown ids are not found.</summary>
    public static async Task<KnowledgeArticle> LoadForWriteAsync(IAppDbContext db, Guid id, uint version, CancellationToken cancellationToken)
    {
        var article = await db.KnowledgeArticles.SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.KnowledgeArticle);
        db.SetExpectedVersion(article, version);
        return article;
    }

    public static object Snapshot(KnowledgeArticle a) => new { a.Title, a.Status, a.Visibility, a.CategoryId, a.Revision };
}
