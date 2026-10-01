using Tgysm.Application.Abstractions;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

/// <summary>
/// Common frame of a ticket action: load in scope (404 outside), check the expected version, run the change,
/// save once (ticket, history, timeline, audit together) and return the fresh detail.
/// </summary>
public sealed class TicketWork(IAppDbContext db, TicketScope scope, Tgysm.Application.Sla.TicketSlaReader sla, TimeProvider clock)
{
    public async Task<TicketDetail> RunAsync(
        string number, uint version, bool requireWorker, Func<Ticket, TicketViewer, DateTimeOffset, Task> change, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = requireWorker
            ? await scope.LoadForWorkAsync(number, cancellationToken)
            : await scope.LoadAsync(number, cancellationToken);
        db.SetExpectedVersion(ticket, version);
        await change(ticket, viewer, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return await TicketQueries.LoadDetailAsync(db, sla, ticket.Id, viewer, cancellationToken);
    }
}
