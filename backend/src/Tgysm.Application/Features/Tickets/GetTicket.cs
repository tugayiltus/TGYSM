namespace Tgysm.Application.Features.Tickets;

public sealed record GetTicketQuery(string Number);

public sealed class GetTicketHandler(Tgysm.Application.Abstractions.IAppDbContext db, TicketScope scope)
{
    public async Task<TicketDetail> HandleAsync(GetTicketQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await TicketQueries.LoadDetailAsync(db, ticket.Id, viewer, cancellationToken);
    }
}
