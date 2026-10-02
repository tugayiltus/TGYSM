using System.Linq.Expressions;
using Tgysm.Domain.Users;

namespace Tgysm.Domain.Knowledge;

/// <summary>Who can read an article (spec §6.2): employees only published employee-visible ones, IT staff and admins all.</summary>
public static class ArticleAccess
{
    public static bool CanRead(ArticleStatus status, ArticleVisibility visibility, UserRole role) =>
        role != UserRole.Employee || (status == ArticleStatus.Published && visibility == ArticleVisibility.Employees);

    /// <summary>Query form of <see cref="CanRead"/>; translates to SQL.</summary>
    public static Expression<Func<KnowledgeArticle, bool>> ReadableBy(UserRole role) =>
        role == UserRole.Employee
            ? article => article.Status == ArticleStatus.Published && article.Visibility == ArticleVisibility.Employees
            : article => true;
}
