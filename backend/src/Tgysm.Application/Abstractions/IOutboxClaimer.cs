using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Abstractions;

/// <summary>
/// Claims a batch of pending <see cref="OutboxEvent"/> rows for exclusive processing (ADR-0014's row-lease
/// pattern, applied to <c>notification_outbox</c>): two worker instances calling this at once get disjoint rows.
/// The claim itself is raw SQL (<c>FOR UPDATE SKIP LOCKED</c>), which is why this is a port rather than something
/// <see cref="IAppDbContext"/> exposes directly — the Infrastructure-layer detail stays out of Application.
/// </summary>
public interface IOutboxClaimer
{
    Task<IReadOnlyList<OutboxEvent>> ClaimAsync(int batch, TimeSpan lease, CancellationToken cancellationToken);
}
