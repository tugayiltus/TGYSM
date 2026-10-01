using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Security;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;

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
    /// <summary>
    /// Forgot-password flood guard (final review F6): while a reset link issued less than this long ago is still
    /// unused, a further request for the same account is skipped — no new token, no email. The request path itself
    /// stays identical for every address, so this never reveals whether an account exists.
    /// </summary>
    public static readonly TimeSpan PasswordResetCooldown = TimeSpan.FromMinutes(15);

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
                await DispatchOwnedAsync(scope.ServiceProvider, outboxEvent, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox event {OutboxEventId} ({EventType}) failed to dispatch.", outboxEvent.Id, outboxEvent.EventType);
                await TryRecordFailureAsync(outboxEvent.Id, exception.Message, cancellationToken);
            }

            processed++;
        }

        return processed;
    }

    /// <summary>
    /// Records the failure in a fresh scope and <see cref="IAppDbContext"/> that reloads the event: the failed
    /// scope may still track the half-built deliveries (or tokens) whose save just threw, and saving from it would
    /// throw the same way again. A failure to record is logged, never rethrown, so the rest of the claimed batch is
    /// still processed; the event then stays leased and is re-claimed on expiry. Same pattern as <c>SendDueEmailsHandler</c>.
    /// </summary>
    private async Task TryRecordFailureAsync(Guid outboxEventId, string error, CancellationToken cancellationToken)
    {
        try
        {
            await using var failureScope = scopes.CreateAsyncScope();
            await RecordFailureAsync(failureScope.ServiceProvider, outboxEventId, error, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Recording the failure of outbox event {OutboxEventId} failed.", outboxEventId);
        }
    }

    private async Task RecordFailureAsync(IServiceProvider scoped, Guid outboxEventId, string error, CancellationToken cancellationToken)
    {
        var db = scoped.GetRequiredService<IAppDbContext>();
        var outboxEvent = await db.OutboxEvents.SingleAsync(e => e.Id == outboxEventId, cancellationToken);
        outboxEvent.RecordFailure(error, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Dispatches one claimed event only while this run still owns it. The batch lease can run out before the
    /// event is reached, and another run may then have re-claimed and dispatched it; a second dispatch would add
    /// duplicate deliveries (and, for a reset request, a second token and email). One conditional update — still
    /// pending and still carrying the lease value this run's claim wrote — both proves ownership and row-locks the
    /// event until the dispatch commits, so no other run can take it over in between.
    /// </summary>
    private async Task DispatchOwnedAsync(IServiceProvider scoped, OutboxEvent claimed, CancellationToken cancellationToken)
    {
        var db = scoped.GetRequiredService<IAppDbContext>();
        await db.RunInTransactionAsync(async ct =>
        {
            var token = claimed.LockedUntil;
            var owned = await db.OutboxEvents
                .Where(e => e.Id == claimed.Id && e.Status == OutboxStatus.Pending && e.LockedUntil == token)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.LockedUntil, e => e.LockedUntil), ct);
            if (owned == 0)
            {
                logger.LogInformation("Outbox event {OutboxEventId} was taken over by another run; skipping it.", claimed.Id);
                return;
            }

            await DispatchOneAsync(scoped, db, claimed.Id, ct);
        }, cancellationToken);
    }

    private async Task DispatchOneAsync(IServiceProvider scoped, IAppDbContext db, Guid outboxEventId, CancellationToken cancellationToken)
    {
        var recipients = scoped.GetRequiredService<RecipientResolver>();
        var context = scoped.GetRequiredService<NotificationContextLoader>();

        var outboxEvent = await db.OutboxEvents.SingleAsync(e => e.Id == outboxEventId, cancellationToken);
        var now = clock.GetUtcNow();
        if (outboxEvent.EventType == InternalEventTypes.PasswordResetRequested)
        {
            await ResolvePasswordResetRequestAsync(scoped, db, outboxEvent, now, cancellationToken);
            return;
        }

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

    /// <summary>
    /// The deferred half of forgot-password (ADR-0017 amendment 2026-10-01): an active account gets a reset link
    /// (its token, <c>password_reset</c> event and audit row are written in this same save, with no actor); an
    /// unknown or inactive email is skipped, and so is an account whose last reset link is still unused and younger
    /// than <see cref="PasswordResetCooldown"/>. Either way no delivery is created for the request event itself.
    /// </summary>
    private static async Task ResolvePasswordResetRequestAsync(
        IServiceProvider scoped, IAppDbContext db, OutboxEvent outboxEvent, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var payload = System.Text.Json.JsonDocument.Parse(outboxEvent.Payload);
        var email = payload.RootElement.TryGetProperty(OutboxEvent.RequestedEmailKey, out var value) ? value.GetString() : null;
        var normalizedEmail = email is null ? null : User.NormalizeEmail(email);
        var userId = normalizedEmail is null
            ? null
            : await db.Users.Where(u => u.NormalizedEmail == normalizedEmail && u.IsActive).Select(u => (Guid?)u.Id).SingleOrDefaultAsync(cancellationToken);

        var cooldownStart = now - PasswordResetCooldown;
        var recentlyLinked = userId is { } candidate && await db.PasswordTokens.AnyAsync(
            t => t.UserId == candidate && t.Purpose == PasswordTokenPurpose.Reset && t.UsedAt == null && t.CreatedAt > cooldownStart,
            cancellationToken);

        if (userId is { } id && !recentlyLinked)
        {
            await scoped.GetRequiredService<PasswordTokenIssuer>().IssueAsync(id, PasswordTokenPurpose.Reset, cancellationToken);
            outboxEvent.MarkDispatched(now);
        }
        else
        {
            outboxEvent.MarkSkipped(now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
