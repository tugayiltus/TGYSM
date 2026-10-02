using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Knowledge;

namespace Tgysm.Application.Features.Knowledge;

public sealed record UpdateArticleCommand(Guid Id, string Title, string Summary, string Body, Guid? CategoryId, ArticleVisibility Visibility, uint Version);

internal sealed class UpdateArticleValidator : AbstractValidator<UpdateArticleCommand>
{
    public UpdateArticleValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(ArticleLimits.TitleMaxLength);
        RuleFor(c => c.Summary).NotEmpty().MaximumLength(ArticleLimits.SummaryMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(ArticleLimits.BodyMaxLength);
        RuleFor(c => c.Visibility).IsInEnum();
    }
}

/// <summary>Saves new content as the next revision; a visibility change is audited on its own as well (spec §6.2).</summary>
public sealed class UpdateArticleHandler(IAppDbContext db, IValidator<UpdateArticleCommand> validator, ICurrentActor actor, IAuditLog audit, TimeProvider clock)
{
    public async Task<ArticleDetail> HandleAsync(UpdateArticleCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        var article = await ArticleQueries.LoadForWriteAsync(db, command.Id, command.Version, cancellationToken);
        var categoryName = await ArticleQueries.ActiveCategoryNameAsync(db, command.CategoryId, cancellationToken);

        var before = ArticleQueries.Snapshot(article);
        var previousVisibility = article.Visibility;
        var now = clock.GetUtcNow();
        article.Update(command.Title, command.Summary, command.Body, command.CategoryId, categoryName, command.Visibility, actorId, now);
        db.ArticleRevisions.Add(ArticleRevision.From(article, now));
        audit.Record(AuditActions.KnowledgeUpdated, AuditEntityTypes.KnowledgeArticle, article.Id, before, ArticleQueries.Snapshot(article));
        if (previousVisibility != article.Visibility)
        {
            audit.Record(
                AuditActions.KnowledgeVisibilityChanged, AuditEntityTypes.KnowledgeArticle, article.Id,
                new { Visibility = previousVisibility }, new { article.Visibility });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ArticleQueries.LoadDetailAsync(db, article.Id, cancellationToken);
    }
}
