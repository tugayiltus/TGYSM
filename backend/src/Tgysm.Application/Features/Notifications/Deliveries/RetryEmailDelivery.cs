using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Notifications.Deliveries;

public sealed record RetryEmailDeliveryCommand(Guid Id);

/// <summary>
/// Admin retry of one <c>failed</c> delivery (spec §3.2 step 4): resets attempts and puts it due immediately, so
/// the next <c>EmailSenderJob</c> run sends it. Anything other than <c>failed</c> is a 409 (<see cref="Tgysm.Domain.Notifications.EmailDelivery.Retry"/>).
/// </summary>
public sealed class RetryEmailDeliveryHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock)
{
    public async Task<EmailDeliveryItem> HandleAsync(RetryEmailDeliveryCommand command, CancellationToken cancellationToken)
    {
        var delivery = await db.EmailDeliveries.SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.EmailDelivery);

        var before = new { delivery.Status, delivery.Attempts };
        delivery.Retry(clock.GetUtcNow());
        audit.Record(AuditActions.EmailDeliveryRetried, AuditEntityTypes.EmailDelivery, delivery.Id, before,
            new { delivery.Status, delivery.Attempts });

        var outboxEvent = await db.OutboxEvents.AsNoTracking().SingleAsync(e => e.Id == delivery.OutboxId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return EmailDeliveryItem.From(delivery, outboxEvent.CreatedAt);
    }
}
