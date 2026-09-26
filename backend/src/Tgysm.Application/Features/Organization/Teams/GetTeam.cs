using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record GetTeamQuery(Guid Id);

public sealed class GetTeamHandler(IAppDbContext db)
{
    public Task<TeamDetail> HandleAsync(GetTeamQuery query, CancellationToken cancellationToken) =>
        TeamQueries.LoadDetailAsync(db, query.Id, cancellationToken);
}
