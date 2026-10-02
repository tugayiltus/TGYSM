using System.Text.RegularExpressions;

namespace Tgysm.Application.Features.Knowledge;

/// <summary>Finds knowledge base article links (<c>/kb/{id}</c>) in free text such as ticket replies (spec §6.2).</summary>
public static class KnowledgeLinks
{
    private const string ArticleLinkPattern = @"/kb/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\b";
    private const int MatchTimeoutMilliseconds = 250;

    private static readonly Regex ArticleLink = new(
        ArticleLinkPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(MatchTimeoutMilliseconds));

    /// <summary>Distinct article ids referenced by absolute or relative <c>/kb/{uuid}</c> links; malformed ids are ignored.</summary>
    public static IReadOnlySet<Guid> ExtractArticleIds(string body) =>
        ArticleLink.Matches(body).Select(m => Guid.Parse(m.Groups[1].Value)).ToHashSet();
}
