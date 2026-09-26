using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Features.Organization.Locations;

/// <summary>Returns the whole location tree as a flat list; the UI builds the tree.</summary>
public sealed class ListLocationsHandler(IAppDbContext db)
{
    /// <summary>Reference data stays small; the cap protects the endpoint from unbounded responses.</summary>
    public const int MaxLocations = 2000;

    public async Task<IReadOnlyList<LocationItem>> HandleAsync(CancellationToken cancellationToken) =>
        await db.Locations.AsNoTracking()
            .OrderBy(l => l.Name).ThenBy(l => l.Id)
            .Take(MaxLocations)
            .Select(l => new LocationItem(l.Id, l.Name, l.ParentId, l.IsActive, l.Version))
            .ToListAsync(cancellationToken);
}
