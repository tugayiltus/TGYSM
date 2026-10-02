namespace Tgysm.Domain.Knowledge;

/// <summary>Append-only snapshot of an article, one per save (spec §6.1).</summary>
public sealed class ArticleRevision
{
    private ArticleRevision()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ArticleId { get; private init; }
    public int Revision { get; private init; }
    public string Title { get; private init; } = string.Empty;
    public string Summary { get; private init; } = string.Empty;
    public string Body { get; private init; } = string.Empty;
    public ArticleVisibility Visibility { get; private init; }
    public Guid? CategoryId { get; private init; }
    public Guid EditedBy { get; private init; }
    public DateTimeOffset EditedAt { get; private init; }

    public static ArticleRevision From(KnowledgeArticle article, DateTimeOffset now) =>
        new()
        {
            ArticleId = article.Id,
            Revision = article.Revision,
            Title = article.Title,
            Summary = article.Summary,
            Body = article.Body,
            Visibility = article.Visibility,
            CategoryId = article.CategoryId,
            EditedBy = article.UpdatedBy,
            EditedAt = now,
        };
}
