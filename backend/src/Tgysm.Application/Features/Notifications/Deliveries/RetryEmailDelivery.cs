using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Notifications.Deliveries;

public sealed record RetryEmailDeliveryCommand(Guid Id);

/// <summary>
/// Admin retry of one <c>failed</c> delivery (spec §3.2 step 4): resets attempts and puts it due immediately, so
/// the next <c>EmailSenderJob</c> run sends it. Anything other than <c>failed</c> is a 409 (<see cref="EmailDelivery.Retry"/>).
/// An account-link delivery lost its token when it failed, so its retry issues a new link (owner decision, final
/// review F4) — and is refused when the user is no longer active or no longer has the address it was queued for.
/// </summary>
public sealed class RetryEmailDeliveryHandler(IAppDbContext db, PasswordTokenIssuer tokens, IAuditLog audit, TimeProvider clock)
{
    private const string RecipientUnavailableMessage = "The recipient is no longer active or no longer has this email address.";

    public async Task<EmailDeliveryItem> HandleAsync(RetryEmailDeliveryCommand command, CancellationToken cancellationToken)
    {
        var delivery = await db.EmailDeliveries.SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.EmailDelivery);

        var before = new { delivery.Status, delivery.Attempts };
        delivery.Retry(clock.GetUtcNow());
        audit.Record(AuditActions.EmailDeliveryRetried, AuditEntityTypes.EmailDelivery, delivery.Id, before,
            new { delivery.Status, delivery.Attempts });

        var outboxEvent = await db.OutboxEvents.SingleAsync(e => e.Id == delivery.OutboxId, cancellationToken);
        if (delivery.TemplateType is NotificationEventTypes.AccountActivation or NotificationEventTypes.PasswordReset)
        {
            var normalizedEmail = User.NormalizeEmail(delivery.RecipientEmail);
            var recipientAvailable = await db.Users.AnyAsync(
                u => u.Id == delivery.RecipientUserId && u.IsActive && u.NormalizedEmail == normalizedEmail, cancellationToken);
            if (!recipientAvailable)
            {
                throw new DomainException(ErrorCodes.EmailDeliveryRecipientUnavailable, RecipientUnavailableMessage);
            }

            await tokens.ReissueAsync(outboxEvent, delivery.RecipientUserId, PasswordTokenIssuer.PurposeOf(delivery.TemplateType), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return EmailDeliveryItem.From(delivery, outboxEvent.CreatedAt);
    }
}
