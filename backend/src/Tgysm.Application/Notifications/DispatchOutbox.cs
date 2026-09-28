using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Worker-side use case behind <c>OutboxDispatcherJob</c> (ADR-0014, ADR-0015, spec §3.2 step 2): claims a batch of
/// pending outbox events, resolves current recipients, renders subject and body from the event's template and
/// inserts <see cref="EmailDelivery"/> rows, then marks the event dispatched or skipped — one transaction per
/// event, so a failure on one event never rolls back another's already-committed deliveries.
/// </summary>
public sealed class DispatchOutboxHandler(IAppDbContext db, IOutboxClaimer claimer, RecipientResolver recipients, NotificationContextLoader context, TimeProvider clock)
{
    public async Task<int> HandleAsync(int batch, TimeSpan lease, CancellationToken cancellationToken)
    {
        // Two worker instances calling HandleAsync at once claim disjoint rows (ADR-0014), so the same event is
        // never dispatched twice; the unique (outbox_id, recipient_user_id) index on email_deliveries is a second
        // line of defense, not the only one.
        var claimed = await claimer.ClaimAsync(batch, lease, cancellationToken);
        var dispatched = 0;
        foreach (var outboxEvent in claimed)
        {
            await DispatchOneAsync(outboxEvent, cancellationToken);
            dispatched++;
        }

        return dispatched;
    }

    private async Task DispatchOneAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var resolved = await recipients.ResolveAsync(outboxEvent, cancellationToken);
        if (resolved.Count == 0)
        {
            outboxEvent.MarkSkipped(now);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var template = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.EventType == outboxEvent.EventType, cancellationToken);
        var isAccountEvent = outboxEvent.EventType is NotificationEventTypes.AccountActivation or NotificationEventTypes.PasswordReset;

        foreach (var recipient in resolved)
        {
            var values = await context.LoadAsync(outboxEvent, recipient, cancellationToken);
            var subject = TemplateRenderer.Render(template.Subject, values);
            // Account-link bodies render at send time from the protected secret (ADR-0015); nothing here ever sees it.
            var body = isAccountEvent ? null : TemplateRenderer.Render(template.Body, values);
            db.EmailDeliveries.Add(EmailDelivery.Create(outboxEvent.Id, recipient, outboxEvent.EventType, subject, body, now));
        }

        outboxEvent.MarkDispatched(now);
        await db.SaveChangesAsync(cancellationToken);
    }
}
