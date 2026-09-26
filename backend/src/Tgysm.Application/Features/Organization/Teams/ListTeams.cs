using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record ListTeamsQuery(string? Search, bool? IsActive, int Page, int PageSize);

public sealed class ListTeamsHandler(IAppDbContext db)
{
    public Task<PagedResult<TeamItem>> HandleAsync(ListTeamsQuery query, CancellationToken cancellationToken)
    {
        var teams = db.Teams.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var key = TurkishText.ToSearchKey(query.Search);
            teams = teams.Where(t => t.NormalizedName.Contains(key));
        }

        if (query.IsActive is { } isActive)
        {
            teams = teams.Where(t => t.IsActive == isActive);
        }

        return teams
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .Select(t => new TeamItem(t.Id, t.Name, t.IsActive, db.TeamMemberships.Count(m => m.TeamId == t.Id), t.Version))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
