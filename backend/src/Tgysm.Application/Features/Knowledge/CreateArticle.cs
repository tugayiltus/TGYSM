using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Knowledge;

namespace Tgysm.Application.Features.Knowledge;

public sealed record CreateArticleCommand(string Title, string Summary, string Body, Guid? CategoryId, ArticleVisibility Visibility);

internal sealed class CreateArticleValidator : AbstractValidator<CreateArticleCommand>
{
    public CreateArticleValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(ArticleLimits.TitleMaxLength);
        RuleFor(c => c.Summary).NotEmpty().MaximumLength(ArticleLimits.SummaryMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(ArticleLimits.BodyMaxLength);
        RuleFor(c => c.Visibility).IsInEnum();
    }
}

public sealed class CreateArticleHandler(IAppDbContext db, IValidator<CreateArticleCommand> validator, ICurrentActor actor, IAuditLog audit, TimeProvider clock)
{
    public async Task<ArticleDetail> HandleAsync(CreateArticleCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        var categoryName = await ArticleQueries.ActiveCategoryNameAsync(db, command.CategoryId, cancellationToken);

        var now = clock.GetUtcNow();
        var article = KnowledgeArticle.Create(command.Title, command.Summary, command.Body, command.CategoryId, categoryName, command.Visibility, actorId, now);
        db.KnowledgeArticles.Add(article);
        db.ArticleRevisions.Add(ArticleRevision.From(article, now));
        audit.Record(AuditActions.KnowledgeCreated, AuditEntityTypes.KnowledgeArticle, article.Id, newValues: ArticleQueries.Snapshot(article));
        await db.SaveChangesAsync(cancellationToken);
        return await ArticleQueries.LoadDetailAsync(db, article.Id, cancellationToken);
    }
}
