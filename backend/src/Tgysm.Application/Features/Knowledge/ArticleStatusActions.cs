using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Knowledge;

namespace Tgysm.Application.Features.Knowledge;

public sealed record PublishArticleCommand(Guid Id, uint Version);

public sealed record ArchiveArticleCommand(Guid Id, uint Version);

public sealed record RestoreArticleCommand(Guid Id, uint Version);

/// <summary>Status changes keep the revision number and write no revision row; the audit row holds the before/after status.</summary>
internal static class ArticleStatusChange
{
    public static async Task<ArticleDetail> ApplyAsync(
        IAppDbContext db, ICurrentActor actor, IAuditLog audit, Guid id, uint version, string action, Action<KnowledgeArticle, Guid> change, CancellationToken cancellationToken)
    {
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        var article = await ArticleQueries.LoadForWriteAsync(db, id, version, cancellationToken);
        var before = article.Status;
        change(article, actorId);
        audit.Record(action, AuditEntityTypes.KnowledgeArticle, article.Id, new { Status = before }, new { article.Status });
        await db.SaveChangesAsync(cancellationToken);
        return await ArticleQueries.LoadDetailAsync(db, article.Id, cancellationToken);
    }
}

public sealed class PublishArticleHandler(IAppDbContext db, ICurrentActor actor, IAuditLog audit, TimeProvider clock)
{
    public Task<ArticleDetail> HandleAsync(PublishArticleCommand command, CancellationToken cancellationToken) =>
        ArticleStatusChange.ApplyAsync(db, actor, audit, command.Id, command.Version, AuditActions.KnowledgePublished, (a, by) => a.Publish(by, clock.GetUtcNow()), cancellationToken);
}

public sealed class ArchiveArticleHandler(IAppDbContext db, ICurrentActor actor, IAuditLog audit, TimeProvider clock)
{
    public Task<ArticleDetail> HandleAsync(ArchiveArticleCommand command, CancellationToken cancellationToken) =>
        ArticleStatusChange.ApplyAsync(db, actor, audit, command.Id, command.Version, AuditActions.KnowledgeArchived, (a, by) => a.Archive(by, clock.GetUtcNow()), cancellationToken);
}

public sealed class RestoreArticleHandler(IAppDbContext db, ICurrentActor actor, IAuditLog audit, TimeProvider clock)
{
    public Task<ArticleDetail> HandleAsync(RestoreArticleCommand command, CancellationToken cancellationToken) =>
        ArticleStatusChange.ApplyAsync(db, actor, audit, command.Id, command.Version, AuditActions.KnowledgeRestored, (a, by) => a.Restore(by, clock.GetUtcNow()), cancellationToken);
}
