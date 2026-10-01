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

    /// <summary>
    /// Re-claims one row of a batch right before it is sent: one conditional update that renews its lease to
    /// <paramref name="lease"/> from now, but only while the row is still <c>pending</c>/<c>sending</c> and still
    /// carries <paramref name="claimedLockedUntil"/> — the lease value this caller's batch claim (or an earlier
    /// re-claim) wrote, used as an ownership token. Returns the new lease value (the next token), or <c>null</c>
    /// when another run has taken the row over or finished it since, in which case the caller must skip it.
    /// </summary>
    Task<DateTimeOffset?> ClaimOneAsync(Guid deliveryId, DateTimeOffset claimedLockedUntil, TimeSpan lease, CancellationToken cancellationToken);
}
