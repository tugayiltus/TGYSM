using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;

namespace Tgysm.Application.Features.Audit;

public sealed record AuditEventItem(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid? ActorUserId,
    string? ActorName,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? Reason,
    string? OldValues,
    string? NewValues,
    string CorrelationId);

public sealed record ListAuditEventsQuery(
    string? Action,
    string? EntityType,
    Guid? EntityId,
    Guid? ActorUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);

public sealed class ListAuditEventsHandler(IAppDbContext db)
{
    public Task<PagedResult<AuditEventItem>> HandleAsync(ListAuditEventsQuery query, CancellationToken cancellationToken)
    {
        var events = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            events = events.Where(e => e.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            events = events.Where(e => e.EntityType == query.EntityType);
        }

        if (query.EntityId is { } entityId)
        {
            events = events.Where(e => e.EntityId == entityId);
        }

        if (query.ActorUserId is { } actorUserId)
        {
            events = events.Where(e => e.ActorUserId == actorUserId);
        }

        if (query.From is { } from)
        {
            events = events.Where(e => e.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            events = events.Where(e => e.OccurredAt <= to);
        }

        var items =
            from auditEvent in events
            join actor in db.Users on auditEvent.ActorUserId equals (Guid?)actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby auditEvent.OccurredAt descending, auditEvent.Id descending
            select new AuditEventItem(
                auditEvent.Id,
                auditEvent.OccurredAt,
                auditEvent.ActorUserId,
                actor == null ? null : actor.FirstName + " " + actor.LastName,
                auditEvent.Action,
                auditEvent.EntityType,
                auditEvent.EntityId,
                auditEvent.Reason,
                auditEvent.OldValues,
                auditEvent.NewValues,
                auditEvent.CorrelationId);

        return items.ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
