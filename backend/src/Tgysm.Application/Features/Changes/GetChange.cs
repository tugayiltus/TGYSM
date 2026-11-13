namespace Tgysm.Application.Features.Changes;

public sealed record GetChangeQuery(string Number);

public sealed class GetChangeHandler(Abstractions.IAppDbContext db, ChangeScope scope)
{
    public async Task<ChangeView> HandleAsync(GetChangeQuery query, CancellationToken cancellationToken)
    {
        var (change, viewer, access) = await scope.LoadAsync(query.Number, cancellationToken);
        return await ChangeQueries.LoadViewAsync(db, change.Id, viewer, access, cancellationToken);
    }
}
