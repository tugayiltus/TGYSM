using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Worker-side use case behind <c>EmailSenderJob</c> (ADR-0014, ADR-0015, spec §3.2 step 3): claims a batch of due
/// <see cref="EmailDelivery"/> rows, then sends each one — render an account-link body at send time when needed,
/// hand it to <see cref="IEmailSender"/>, mark it sent or record the failure — in its own DI scope and its own
/// <see cref="IAppDbContext"/>, one delivery per scope. Same reasoning as <c>DispatchOutboxHandler</c>: a fresh
/// scope per delivery means one delivery's failure (a bad recipient, an unprotectable secret, an SMTP outage)
/// cannot silently wipe another already-claimed delivery's own not-yet-saved success in the same batch, and
/// <c>IEmailDeliveryClaimer</c> claims in id order, so a single permanently-failing row cannot starve the rest.
/// </summary>
public sealed class SendDueEmailsHandler(
    IEmailDeliveryClaimer claimer,
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<SendDueEmailsHandler> logger)
{
    public async Task<int> HandleAsync(int batch, TimeSpan lease, CancellationToken cancellationToken)
    {
        // Two worker instances calling HandleAsync at once claim disjoint rows (ADR-0014); the claimed entities
        // are only used for their id here, every actual read and write happens through a fresh per-delivery scope.
        var claimed = await claimer.ClaimAsync(batch, lease, cancellationToken);
        var processed = 0;
        foreach (var delivery in claimed)
        {
            await using var scope = scopes.CreateAsyncScope();
            try
            {
                await SendOneAsync(scope.ServiceProvider, delivery.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Email delivery {DeliveryId} failed to send.", delivery.Id);
                await TryRecordFailureAsync(delivery.Id, exception.Message, cancellationToken);
            }

            processed++;
        }

        return processed;
    }

    /// <summary>
    /// Records the failure in a fresh scope and <see cref="IAppDbContext"/> that reloads the row: the failed
    /// attempt's context may still track unsaved Sent/cleared state (e.g. the final save threw after the transport
    /// succeeded), and saving from it would persist that state. A failure to record is logged, never rethrown, so
    /// the rest of the claimed batch is still processed; the row then stays leased and is re-claimed on expiry.
    /// </summary>
    private async Task TryRecordFailureAsync(Guid deliveryId, string error, CancellationToken cancellationToken)
    {
        try
        {
            await using var failureScope = scopes.CreateAsyncScope();
            await RecordFailureAsync(failureScope.ServiceProvider, deliveryId, error, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Recording the failure of email delivery {DeliveryId} failed.", deliveryId);
        }
    }

    private async Task RecordFailureAsync(IServiceProvider scoped, Guid deliveryId, string error, CancellationToken cancellationToken)
    {
        var db = scoped.GetRequiredService<IAppDbContext>();
        var delivery = await db.EmailDeliveries.SingleAsync(d => d.Id == deliveryId, cancellationToken);
        delivery.RecordFailure(error, clock.GetUtcNow());

        // Only a *terminal* failure clears the account-link secret/body (spec §3.2 step 5): a still-retryable
        // failure must leave the token in place, or the next retry could never render the link again.
        if (delivery.Status == DeliveryStatus.Failed && IsAccountEvent(delivery.TemplateType))
        {
            var outboxEvent = await db.OutboxEvents.SingleAsync(e => e.Id == delivery.OutboxId, cancellationToken);
            delivery.ClearBody();
            outboxEvent.ClearProtectedSecret();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SendOneAsync(IServiceProvider scoped, Guid deliveryId, CancellationToken cancellationToken)
    {
        var db = scoped.GetRequiredService<IAppDbContext>();
        var sender = scoped.GetRequiredService<IEmailSender>();

        var delivery = await db.EmailDeliveries.SingleAsync(d => d.Id == deliveryId, cancellationToken);
        delivery.MarkSending(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        var isAccountEvent = IsAccountEvent(delivery.TemplateType);
        var outboxEvent = isAccountEvent
            ? await db.OutboxEvents.SingleAsync(e => e.Id == delivery.OutboxId, cancellationToken)
            : null;
        var recipient = await db.Users.AsNoTracking().SingleAsync(u => u.Id == delivery.RecipientUserId, cancellationToken);
        var body = isAccountEvent
            ? await RenderAccountBodyAsync(scoped, db, delivery, outboxEvent!, recipient, cancellationToken)
            : delivery.Body ?? string.Empty;

        await sender.SendAsync(new EmailMessage(delivery.RecipientEmail, FullNameOf(recipient), delivery.Subject, body), cancellationToken);

        delivery.MarkSent(clock.GetUtcNow());
        if (isAccountEvent)
        {
            delivery.ClearBody();
            outboxEvent!.ClearProtectedSecret();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Account-link bodies render at send time from the outbox event's <see cref="OutboxEvent.ProtectedSecret"/>
    /// (ADR-0015): nothing before this point ever sees the token. <c>expiresAt</c> is left unset until Task 7
    /// wires token expiry into the account-link flow; <see cref="Domain.Notifications.TemplateRenderer.Render"/>
    /// renders an unset variable as empty text rather than failing.
    /// </summary>
    private static async Task<string> RenderAccountBodyAsync(
        IServiceProvider scoped, IAppDbContext db, EmailDelivery delivery, OutboxEvent outboxEvent, User recipient, CancellationToken cancellationToken)
    {
        var protector = scoped.GetRequiredService<IAccountLinkProtector>();
        var appOptions = scoped.GetRequiredService<IOptionsMonitor<AppOptions>>();

        var secret = outboxEvent.ProtectedSecret
            ?? throw new InvalidOperationException("Account email delivery has no link token left to render.");
        var token = protector.Unprotect(secret);
        var actionUrl = AccountLinkUrl.Build(appOptions.CurrentValue.PublicBaseUrl, token);

        var template = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.EventType == delivery.TemplateType, cancellationToken);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TemplateVariables.RecipientName] = FullNameOf(recipient),
            [TemplateVariables.ActionUrl] = actionUrl,
        };
        return TemplateRenderer.Render(template.Body, values);
    }

    private static bool IsAccountEvent(string templateType) =>
        templateType is NotificationEventTypes.AccountActivation or NotificationEventTypes.PasswordReset;

    private static string FullNameOf(User user) => $"{user.FirstName} {user.LastName}";
}
