using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Infrastructure.Persistence;

/// <summary><see cref="IEmailDeliveryClaimer"/> over <see cref="RowLease"/> (ADR-0014), the same pattern <see cref="OutboxClaimer"/> applies to <c>notification_outbox</c>.</summary>
internal sealed class EmailDeliveryClaimer(IAppDbContext db) : IEmailDeliveryClaimer
{
    // A 'sending' row whose lease has expired belongs to a crashed worker (spec §2): it becomes claimable again.
    private const string DuePredicate =
        "((status = 'pending' AND next_attempt_at <= now()) OR status = 'sending') AND (locked_until IS NULL OR locked_until < now())";

    public async Task<IReadOnlyList<EmailDelivery>> ClaimAsync(int batch, TimeSpan lease, CancellationToken cancellationToken) =>
        await RowLease.ClaimAsync(db.EmailDeliveries, DuePredicate, batch, lease, cancellationToken);
}
