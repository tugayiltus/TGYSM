using Tgysm.Domain.Common;
using Tgysm.Domain.Knowledge;

namespace Tgysm.Domain.Tests.Knowledge;

public sealed class KnowledgeArticleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddDays(1);
    private static readonly DateTimeOffset Latest = Now.AddDays(2);
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly Guid Editor = Guid.NewGuid();

    private static KnowledgeArticle Draft(string title = "VPN kurulumu", string summary = "Özet", string body = "Gövde", string? categoryName = "Ağ") =>
        KnowledgeArticle.Create(title, summary, body, Guid.NewGuid(), categoryName, ArticleVisibility.Employees, Author, Now);

    private static void AssertInvalid(Action action) =>
        Assert.Equal(ErrorCodes.KnowledgeInvalidTransition, Assert.Throws<DomainException>(action).Code);

    [Fact]
    public void Create_is_draft_revision_one_and_trimmed()
    {
        var article = Draft(title: "  VPN kurulumu ");

        Assert.Equal(ArticleStatus.Draft, article.Status);
        Assert.Equal("VPN kurulumu", article.Title);
        Assert.Equal(1, article.Revision);
        Assert.Null(article.PublishedAt);
        Assert.Equal((Author, Now), (article.AuthorId, article.UpdatedAt));
        Assert.Equal(Author, article.UpdatedBy);
    }

    [Fact]
    public void Publish_from_draft_sets_published_at_and_keeps_revision()
    {
        var article = Draft();

        article.Publish(Editor, Later);

        Assert.Equal(ArticleStatus.Published, article.Status);
        Assert.Equal(Later, article.PublishedAt);
        Assert.Equal((Editor, Later, 1), (article.UpdatedBy, article.UpdatedAt, article.Revision));
    }

    [Fact]
    public void Publish_from_archived_or_published_throws()
    {
        var archived = Draft();
        archived.Archive(Editor, Later);
        var published = Draft();
        published.Publish(Editor, Later);

        AssertInvalid(() => archived.Publish(Editor, Latest));
        AssertInvalid(() => published.Publish(Editor, Latest));
    }

    [Fact]
    public void Archive_from_draft_and_published_but_not_twice()
    {
        var fromDraft = Draft();
        var fromPublished = Draft();
        fromPublished.Publish(Editor, Later);

        fromDraft.Archive(Editor, Later);
        fromPublished.Archive(Editor, Latest);

        Assert.Equal(ArticleStatus.Archived, fromDraft.Status);
        Assert.Equal(ArticleStatus.Archived, fromPublished.Status);
        AssertInvalid(() => fromDraft.Archive(Editor, Latest));
    }

    [Fact]
    public void Restore_only_from_archived_and_lands_on_draft()
    {
        var article = Draft();
        AssertInvalid(() => article.Restore(Editor, Later));
        article.Archive(Editor, Later);

        article.Restore(Editor, Latest);

        Assert.Equal(ArticleStatus.Draft, article.Status);
    }

    [Fact]
    public void Republish_keeps_first_published_at()
    {
        var article = Draft();
        article.Publish(Editor, Later);
        article.Archive(Editor, Latest);
        article.Restore(Editor, Latest.AddDays(1));

        article.Publish(Editor, Latest.AddDays(2));

        Assert.Equal(Later, article.PublishedAt);
    }

    [Fact]
    public void Update_rewrites_fields_search_text_and_revision()
    {
        var article = Draft();
        var categoryId = Guid.NewGuid();

        article.Update("Yeni başlık", "Yeni özet", "Yeni gövde", categoryId, "Yazıcı", ArticleVisibility.Internal, Editor, Later);

        Assert.Equal(("Yeni başlık", "Yeni özet", "Yeni gövde"), (article.Title, article.Summary, article.Body));
        Assert.Equal((categoryId, ArticleVisibility.Internal, Editor, Later, 2), (article.CategoryId, article.Visibility, article.UpdatedBy, article.UpdatedAt, article.Revision));
        Assert.Contains("yazici", article.SearchText);
    }

    [Fact]
    public void Search_key_folds_dotted_and_dotless_i_and_includes_category_name()
    {
        var article = Draft(title: "IŞIK İnternet", summary: "ılık", body: "Iİıi", categoryName: "Ağ");

        Assert.Equal(TurkishText.ToSearchKey("IŞIK İnternet ılık Iİıi Ağ"), article.SearchText);
        Assert.StartsWith("işik internet", article.SearchText);
        Assert.DoesNotContain('ı', article.SearchText);
        Assert.EndsWith("ağ", article.SearchText);
    }

    [Fact]
    public void Search_key_without_category_has_no_null_text()
    {
        var article = Draft(categoryName: null);

        Assert.DoesNotContain("null", article.SearchText);
    }

    [Fact]
    public void Status_changes_keep_search_text_and_revision()
    {
        var article = Draft();
        var before = article.SearchText;

        article.Publish(Editor, Later);

        Assert.Equal(before, article.SearchText);
        Assert.Equal(1, article.Revision);
    }

    [Fact]
    public void Archive_and_restore_keep_revision()
    {
        var article = Draft();

        article.Archive(Editor, Later);
        article.Restore(Editor, Latest);

        Assert.Equal(1, article.Revision);
    }

    [Fact]
    public void Update_on_archived_article_throws_until_restored()
    {
        var article = Draft();
        article.Archive(Editor, Later);

        AssertInvalid(() => article.Update("t", "s", "b", null, null, ArticleVisibility.Employees, Editor, Latest));

        article.Restore(Editor, Latest);
        article.Update("t", "s", "b", null, null, ArticleVisibility.Employees, Editor, Latest.AddDays(1));
        Assert.Equal(2, article.Revision);
    }

    [Theory]
    [InlineData(ArticleLimits.TitleMaxLength + 1, 1, 1)]
    [InlineData(1, ArticleLimits.SummaryMaxLength + 1, 1)]
    [InlineData(1, 1, ArticleLimits.BodyMaxLength + 1)]
    public void Over_limit_fields_throw(int titleLength, int summaryLength, int bodyLength)
    {
        var title = new string('a', titleLength);
        var summary = new string('b', summaryLength);
        var body = new string('c', bodyLength);

        var create = Assert.Throws<DomainException>(() => KnowledgeArticle.Create(title, summary, body, null, null, ArticleVisibility.Employees, Author, Now));
        var update = Assert.Throws<DomainException>(() => Draft().Update(title, summary, body, null, null, ArticleVisibility.Employees, Editor, Later));

        Assert.Equal((ErrorCodes.ValidationFailed, ErrorCodes.ValidationFailed), (create.Code, update.Code));
    }

    [Fact]
    public void Limits_at_the_boundary_are_accepted()
    {
        var article = KnowledgeArticle.Create(
            new string('a', ArticleLimits.TitleMaxLength), new string('b', ArticleLimits.SummaryMaxLength), new string('c', ArticleLimits.BodyMaxLength),
            null, null, ArticleVisibility.Employees, Author, Now);

        Assert.Equal(ArticleLimits.BodyMaxLength, article.Body.Length);
    }

    [Fact]
    public void Revision_snapshot_copies_current_content_and_editor()
    {
        var article = Draft();
        article.Update("Başlık", "Özet", "Gövde", null, null, ArticleVisibility.Internal, Editor, Later);

        var revision = ArticleRevision.From(article, Later);

        Assert.Equal((article.Id, 2, "Başlık", "Özet", "Gövde"), (revision.ArticleId, revision.Revision, revision.Title, revision.Summary, revision.Body));
        Assert.Equal((ArticleVisibility.Internal, article.CategoryId, Editor, Later), (revision.Visibility, revision.CategoryId, revision.EditedBy, revision.EditedAt));
    }
}
