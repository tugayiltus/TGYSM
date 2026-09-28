using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Abstractions;

/// <summary>
/// Claims a batch of due <see cref="EmailDelivery"/> rows for exclusive processing (ADR-0014's row-lease pattern,
/// the same one <see cref="IOutboxClaimer"/> applies to <c>notification_outbox</c>, applied here to
/// <c>email_deliveries</c>): two worker instances calling this at once get disjoint rows. Raw SQL
/// (<c>FOR UPDATE SKIP LOCKED</c>) stays an Infrastructure detail behind this port.
/// </summary>
public interface IEmailDeliveryClaimer
{
    Task<IReadOnlyList<EmailDelivery>> ClaimAsync(int batch, TimeSpan lease, CancellationToken cancellationToken);
}
