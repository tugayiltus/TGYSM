using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Worker-side use case behind <c>OutboxDispatcherJob</c> (ADR-0014, ADR-0015, spec §3.2 step 2): claims a batch of
/// pending outbox events, then dispatches each one — resolve current recipients, render subject and body from the
/// event's template, insert <see cref="EmailDelivery"/> rows, mark the event dispatched or skipped — in its own DI
/// scope and its own <see cref="IAppDbContext"/>, one event per scope. This is not just "a transaction per event":
/// a fresh scope per event is what makes one event's failure genuinely not touch another's. A shared context across
/// the whole batch would mean that after a caught failure, discarding the failed event's half-applied tracked
/// changes (a documented recovery step — the project's .NET gotchas guide: "after a failed SaveChanges, that
/// context is not a good place to write from") also discards every *other* claimed event's already-tracked-but-not-
/// yet-saved state in the same call — including a later event's own successful <c>MarkDispatched</c>, which would
/// then silently never reach the database. A fresh scope per event has no such shared state to accidentally wipe.
/// A single bad event (unknown event type, a ticket deleted out from under it, a bad `App:TimeZone`, ...) is caught
/// and recorded as a failed attempt on that event alone (<see cref="OutboxEvent.RecordFailure"/>) rather than
/// aborting the batch: without this, since <c>RowLease</c> claims in id (creation) order, one permanently-failing
/// event would be re-claimed first on every run and starve every event behind it forever.
/// </summary>
public sealed class DispatchOutboxHandler(IOutboxClaimer claimer, IServiceScopeFactory scopes, TimeProvider clock, ILogger<DispatchOutboxHandler> logger)
{
    public async Task<int> HandleAsync(int batch, TimeSpan lease, CancellationToken cancellationToken)
    {
        // Two worker instances calling HandleAsync at once claim disjoint rows (ADR-0014), so the same event is
        // never dispatched twice; the unique (outbox_id, recipient_user_id) index on email_deliveries is a second
        // line of defense, not the only one. The claimed entities themselves are only used for their id/event type
        // here — every actual read and write happens through a fresh per-event scope below.
        var claimed = await claimer.ClaimAsync(batch, lease, cancellationToken);
        var processed = 0;
        foreach (var outboxEvent in claimed)
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await DispatchOneAsync(scope.ServiceProvider, outboxEvent.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox event {OutboxEventId} ({EventType}) failed to dispatch.", outboxEvent.Id, outboxEvent.EventType);
                await RecordFailureAsync(scope.ServiceProvider, outboxEvent.Id, exception.Message, cancellationToken);
            }

            processed++;
        }

        return processed;
    }

    private async Task RecordFailureAsync(IServiceProvider scoped, Guid outboxEventId, string error, CancellationToken cancellationToken)
    {
        var db = scoped.GetRequiredService<IAppDbContext>();
        var outboxEvent = await db.OutboxEvents.SingleAsync(e => e.Id == outboxEventId, cancellationToken);
        outboxEvent.RecordFailure(error, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchOneAsync(IServiceProvider scoped, Guid outboxEventId, CancellationToken cancellationToken)
    {
        var db = scoped.GetRequiredService<IAppDbContext>();
        var recipients = scoped.GetRequiredService<RecipientResolver>();
        var context = scoped.GetRequiredService<NotificationContextLoader>();

        var outboxEvent = await db.OutboxEvents.SingleAsync(e => e.Id == outboxEventId, cancellationToken);
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
