using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Infrastructure.Persistence;

/// <summary><see cref="IEmailDeliveryClaimer"/> over <see cref="RowLease"/> (ADR-0014), the same pattern <see cref="OutboxClaimer"/> applies to <c>notification_outbox</c>.</summary>
internal sealed class EmailDeliveryClaimer(IAppDbContext db) : IEmailDeliveryClaimer
{
    // A 'sending' row whose lease has expired belongs to a crashed worker (spec §2): it becomes claimable again.
    private const string DuePredicate =
        "((status = 'pending' AND next_attempt_at <= now()) OR status = 'sending') AND (locked_until IS NULL OR locked_until < now())";

    // Lease time comes from the database clock, exactly like RowLease, so both claims compare against one clock.
    private const string ClaimOneSql = @"
        UPDATE email_deliveries SET locked_until = now() + ({0} * interval '1 second')
        WHERE id = {1} AND locked_until = {2} AND status IN ('pending', 'sending')
        RETURNING email_deliveries.*, email_deliveries.xmin";

    public async Task<IReadOnlyList<EmailDelivery>> ClaimAsync(int batch, TimeSpan lease, CancellationToken cancellationToken) =>
        await RowLease.ClaimAsync(db.EmailDeliveries, DuePredicate, batch, lease, cancellationToken);

    public async Task<DateTimeOffset?> ClaimOneAsync(Guid deliveryId, DateTimeOffset claimedLockedUntil, TimeSpan lease, CancellationToken cancellationToken)
    {
        var leaseSeconds = (int)lease.TotalSeconds;
        var claimed = await db.EmailDeliveries
            .FromSqlRaw(ClaimOneSql, leaseSeconds, deliveryId, claimedLockedUntil)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return claimed.Count == 1 ? claimed[0].LockedUntil : null;
    }
}
