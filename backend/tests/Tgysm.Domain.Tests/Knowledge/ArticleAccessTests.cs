using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Users;

namespace Tgysm.Domain.Tests.Knowledge;

public sealed class ArticleAccessTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    public static TheoryData<ArticleStatus, ArticleVisibility, UserRole, bool> Cases()
    {
        var data = new TheoryData<ArticleStatus, ArticleVisibility, UserRole, bool>();
        foreach (var status in Enum.GetValues<ArticleStatus>())
        {
            foreach (var visibility in Enum.GetValues<ArticleVisibility>())
            {
                foreach (var role in Enum.GetValues<UserRole>())
                {
                    var expected = role != UserRole.Employee || (status == ArticleStatus.Published && visibility == ArticleVisibility.Employees);
                    data.Add(status, visibility, role, expected);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CanRead_truth_table(ArticleStatus status, ArticleVisibility visibility, UserRole role, bool expected) =>
        Assert.Equal(expected, ArticleAccess.CanRead(status, visibility, role));

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReadableBy_predicate_matches_CanRead(ArticleStatus status, ArticleVisibility visibility, UserRole role, bool expected)
    {
        var article = KnowledgeArticle.Create("t", "s", "b", null, null, visibility, Guid.NewGuid(), Now);
        if (status == ArticleStatus.Published)
        {
            article.Publish(Guid.NewGuid(), Now);
        }
        else if (status == ArticleStatus.Archived)
        {
            article.Archive(Guid.NewGuid(), Now);
        }

        Assert.Equal(expected, ArticleAccess.ReadableBy(role).Compile()(article));
    }
}
