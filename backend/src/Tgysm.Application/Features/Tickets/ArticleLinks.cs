using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

public sealed record LinkArticleCommand(string Number, Guid ArticleId);

public sealed record UnlinkArticleCommand(string Number, Guid ArticleId);

public sealed record ListTicketArticlesQuery(string Number);

public sealed record TicketArticleItem(Guid ArticleId, string Title, ArticleVisibility Visibility, ArticleStatus Status);

internal static class TicketArticleQueries
{
    public static async Task<IReadOnlyList<TicketArticleItem>> ListAsync(IAppDbContext db, Guid ticketId, TicketViewer viewer, CancellationToken cancellationToken) =>
        await (
            from l in db.TicketArticles.AsNoTracking()
            join a in db.KnowledgeArticles.Where(ArticleAccess.ReadableBy(viewer.Role)) on l.ArticleId equals a.Id
            where l.TicketId == ticketId
            orderby l.LinkedAt, l.Id
            select new TicketArticleItem(a.Id, a.Title, a.Visibility, a.Status)).ToListAsync(cancellationToken);

    /// <summary>Like comments and asset links, a link change touches <c>updated_at</c> without a version check.</summary>
    public static Task SaveWithActivityAsync(IAppDbContext db, Guid ticketId, DateTimeOffset now, CancellationToken cancellationToken) =>
        TicketAssetQueries.SaveWithActivityAsync(db, ticketId, now, cancellationToken);

    /// <summary>Linking and unlinking are work actions: an employee who can see the ticket gets 403, anyone outside scope 404.</summary>
    public static async Task<(Ticket Ticket, TicketViewer Viewer)> LoadForLinkingAsync(TicketScope scope, string number, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(number, cancellationToken);
        return viewer.CanWork(ticket) ? (ticket, viewer) : throw new ForbiddenException();
    }
}

/// <summary>IT staff of the ticket's team (and admins) link a knowledge article; archived articles cannot be linked (spec §6.2).</summary>
public sealed class LinkArticleHandler(IAppDbContext db, TicketScope scope, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<TicketArticleItem>> HandleAsync(LinkArticleCommand command, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await TicketArticleQueries.LoadForLinkingAsync(scope, command.Number, cancellationToken);
        var status = await db.KnowledgeArticles.AsNoTracking().Where(a => a.Id == command.ArticleId).Select(a => (ArticleStatus?)a.Status).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.KnowledgeArticle);
        if (status == ArticleStatus.Archived)
        {
            throw new DomainException(ErrorCodes.KnowledgeInvalidTransition, "An archived article cannot be linked.");
        }

        if (await db.TicketArticles.AnyAsync(l => l.TicketId == ticket.Id && l.ArticleId == command.ArticleId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.KnowledgeArticleAlreadyLinked, "The article is already linked.");
        }

        var now = clock.GetUtcNow();
        db.TicketArticles.Add(TicketArticle.Create(ticket.Id, command.ArticleId, viewer.UserId, now));
        audit.Record(AuditActions.TicketArticleLinked, AuditEntityTypes.Ticket, ticket.Id, newValues: new { command.ArticleId });
        await TicketArticleQueries.SaveWithActivityAsync(db, ticket.Id, now, cancellationToken);
        return await TicketArticleQueries.ListAsync(db, ticket.Id, viewer, cancellationToken);
    }
}

public sealed class UnlinkArticleHandler(IAppDbContext db, TicketScope scope, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<TicketArticleItem>> HandleAsync(UnlinkArticleCommand command, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await TicketArticleQueries.LoadForLinkingAsync(scope, command.Number, cancellationToken);
        var link = await db.TicketArticles.SingleOrDefaultAsync(l => l.TicketId == ticket.Id && l.ArticleId == command.ArticleId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.KnowledgeArticle);
        var now = clock.GetUtcNow();
        db.TicketArticles.Remove(link);
        audit.Record(AuditActions.TicketArticleUnlinked, AuditEntityTypes.Ticket, ticket.Id, new { command.ArticleId });
        await TicketArticleQueries.SaveWithActivityAsync(db, ticket.Id, now, cancellationToken);
        return await TicketArticleQueries.ListAsync(db, ticket.Id, viewer, cancellationToken);
    }
}

/// <summary>Linked articles the viewer may read: the requester sees only published employee-visible ones.</summary>
public sealed class ListTicketArticlesHandler(IAppDbContext db, TicketScope scope)
{
    public async Task<IReadOnlyList<TicketArticleItem>> HandleAsync(ListTicketArticlesQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await TicketArticleQueries.ListAsync(db, ticket.Id, viewer, cancellationToken);
    }
}
