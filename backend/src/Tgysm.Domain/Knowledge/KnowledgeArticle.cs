using Tgysm.Domain.Common;

namespace Tgysm.Domain.Knowledge;

/// <summary>
/// Knowledge base article (spec §6). State machine: draft → published; draft/published → archived; archived → draft.
/// Only <see cref="Create"/> and <see cref="Update"/> (content changes) bump <see cref="Revision"/>, so revisions have no gaps;
/// status changes keep it. Archived articles must be restored before they can be edited.
/// </summary>
public sealed class KnowledgeArticle : Entity
{
    private KnowledgeArticle()
    {
    }

    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public ArticleVisibility Visibility { get; private set; }
    public ArticleStatus Status { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public int Revision { get; private set; }

    /// <summary>Turkish-insensitive key of title, summary, body and category name (<see cref="TurkishText.ToSearchKey"/>).</summary>
    public string SearchText { get; private set; } = string.Empty;

    public static KnowledgeArticle Create(
        string title, string summary, string body, Guid? categoryId, string? categoryName, ArticleVisibility visibility, Guid authorId, DateTimeOffset now)
    {
        var article = new KnowledgeArticle { AuthorId = authorId, Status = ArticleStatus.Draft };
        article.Apply(title, summary, body, categoryId, categoryName, visibility, authorId, now);
        return article;
    }

    public void Update(
        string title, string summary, string body, Guid? categoryId, string? categoryName, ArticleVisibility visibility, Guid editorId, DateTimeOffset now)
    {
        Require([ArticleStatus.Draft, ArticleStatus.Published], nameof(Update));
        Apply(title, summary, body, categoryId, categoryName, visibility, editorId, now);
    }

    public void Publish(Guid actorId, DateTimeOffset now)
    {
        Require([ArticleStatus.Draft], nameof(Publish));
        Status = ArticleStatus.Published;
        PublishedAt ??= now;
        Touch(actorId, now);
    }

    public void Archive(Guid actorId, DateTimeOffset now)
    {
        Require([ArticleStatus.Draft, ArticleStatus.Published], nameof(Archive));
        Status = ArticleStatus.Archived;
        Touch(actorId, now);
    }

    public void Restore(Guid actorId, DateTimeOffset now)
    {
        Require([ArticleStatus.Archived], nameof(Restore));
        Status = ArticleStatus.Draft;
        Touch(actorId, now);
    }

    private void Apply(
        string title, string summary, string body, Guid? categoryId, string? categoryName, ArticleVisibility visibility, Guid editorId, DateTimeOffset now)
    {
        var trimmedTitle = title.Trim();
        var trimmedSummary = summary.Trim();
        var trimmedBody = body.Trim();
        RequireLength(trimmedTitle, ArticleLimits.TitleMaxLength, nameof(title));
        RequireLength(trimmedSummary, ArticleLimits.SummaryMaxLength, nameof(summary));
        RequireLength(trimmedBody, ArticleLimits.BodyMaxLength, nameof(body));

        Title = trimmedTitle;
        Summary = trimmedSummary;
        Body = trimmedBody;
        CategoryId = categoryId;
        Visibility = visibility;
        SearchText = TurkishText.ToSearchKey($"{trimmedTitle} {trimmedSummary} {trimmedBody} {categoryName}");
        Touch(editorId, now);
        Revision++;
    }

    private void Touch(Guid actorId, DateTimeOffset now)
    {
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    private static void RequireLength(string value, int max, string field)
    {
        if (value.Length > max)
        {
            throw new DomainException(ErrorCodes.ValidationFailed, $"'{field}' is longer than {max} characters.");
        }
    }

    private void Require(ArticleStatus[] allowed, string operation)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException(ErrorCodes.KnowledgeInvalidTransition, $"'{operation}' is not allowed while the article is {Status}.");
        }
    }
}
