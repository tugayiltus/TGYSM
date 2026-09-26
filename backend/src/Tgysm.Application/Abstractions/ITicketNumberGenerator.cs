using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Abstractions;

public interface ITicketNumberGenerator
{
    /// <summary>
    /// Next value for kind and year. Runs in the caller's transaction: the counter row stays locked until it commits,
    /// so concurrent creators get distinct, sequential values (ADR-0011).
    /// </summary>
    Task<long> NextAsync(TicketKind kind, int year, CancellationToken cancellationToken);
}
