using Tgysm.Application.Features.Knowledge;

namespace Tgysm.Application.Tests.Knowledge;

public sealed class KnowledgeLinksTests
{
    private static readonly Guid First = Guid.Parse("0192f3a0-1111-7000-8000-000000000001");
    private static readonly Guid Second = Guid.Parse("0192f3a0-2222-7000-8000-000000000002");

    [Fact]
    public void Absolute_link_is_found()
    {
        Assert.Equal([First], KnowledgeLinks.ExtractArticleIds($"Bakın: https://itsm.example.com/kb/{First}"));
    }

    [Fact]
    public void Relative_link_is_found()
    {
        Assert.Equal([First], KnowledgeLinks.ExtractArticleIds($"/kb/{First}"));
    }

    [Fact]
    public void Upper_case_uuid_is_found()
    {
        Assert.Equal([First], KnowledgeLinks.ExtractArticleIds($"/kb/{First.ToString().ToUpperInvariant()}"));
    }

    [Fact]
    public void Two_links_give_two_ids_and_repeats_collapse()
    {
        var ids = KnowledgeLinks.ExtractArticleIds($"/kb/{First} ve https://x.test/kb/{Second} ve yine /kb/{First}");
        Assert.Equal(new HashSet<Guid> { First, Second }, ids);
    }

    [Fact]
    public void Malformed_uuid_is_ignored()
    {
        Assert.Empty(KnowledgeLinks.ExtractArticleIds("/kb/not-a-uuid /kb/0192f3a0-1111-7000-8000-00000000000 /kb/0192f3a0-1111-7000-8000-0000000000012"));
    }
}
