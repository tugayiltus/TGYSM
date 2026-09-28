using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Features.Notifications.Deliveries;

/// <summary>
/// One row of `/admin/email-deliveries` (spec §3.2 step 4). <see cref="Body"/> is the delivery's own stored body —
/// for an account-link delivery that already reached <see cref="DeliveryStatus.Sent"/>/<see cref="DeliveryStatus.Failed"/>
/// this is already <see cref="EmailDelivery.ClearedBodyMarker"/>, and <see cref="EmailDelivery.RecordFailure"/> /
/// <see cref="EmailDelivery.MarkSent"/> never leave the real link body behind, so this projection never needs to
/// special-case account deliveries itself: it simply never has a secret to leak.
/// </summary>
public sealed record EmailDeliveryItem(
    Guid Id,
    Guid OutboxId,
    string EventType,
    Guid RecipientUserId,
    string RecipientEmail,
    string Subject,
    string? Body,
    DeliveryStatus Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    string? LastError,
    DateTimeOffset? SentAt,
    DateTimeOffset EventCreatedAt)
{
    public static EmailDeliveryItem From(EmailDelivery delivery, DateTimeOffset eventCreatedAt) =>
        new(delivery.Id, delivery.OutboxId, delivery.TemplateType, delivery.RecipientUserId, delivery.RecipientEmail,
            delivery.Subject, delivery.Body, delivery.Status, delivery.Attempts, delivery.NextAttemptAt,
            delivery.LastError, delivery.SentAt, eventCreatedAt);
}

public sealed record ListEmailDeliveriesQuery(
    DeliveryStatus? Status,
    string? EventType,
    string? Recipient,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);

public sealed class ListEmailDeliveriesHandler(IAppDbContext db)
{
    public Task<PagedResult<EmailDeliveryItem>> HandleAsync(ListEmailDeliveriesQuery query, CancellationToken cancellationToken)
    {
        var deliveries = db.EmailDeliveries.AsNoTracking();
        if (query.Status is { } status)
        {
            deliveries = deliveries.Where(d => d.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            deliveries = deliveries.Where(d => d.TemplateType == query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(query.Recipient))
        {
            deliveries = deliveries.Where(d => d.RecipientEmail == query.Recipient);
        }

        var items =
            from delivery in deliveries
            join outboxEvent in db.OutboxEvents.AsNoTracking() on delivery.OutboxId equals outboxEvent.Id
            where query.From == null || outboxEvent.CreatedAt >= query.From
            where query.To == null || outboxEvent.CreatedAt <= query.To
            orderby outboxEvent.CreatedAt descending, delivery.Id descending
            select new EmailDeliveryItem(
                delivery.Id, delivery.OutboxId, delivery.TemplateType, delivery.RecipientUserId, delivery.RecipientEmail,
                delivery.Subject, delivery.Body, delivery.Status, delivery.Attempts, delivery.NextAttemptAt,
                delivery.LastError, delivery.SentAt, outboxEvent.CreatedAt);

        return items.ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
