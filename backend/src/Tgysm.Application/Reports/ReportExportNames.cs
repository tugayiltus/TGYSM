using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Reports;

/// <summary>
/// Names for the ids an export's "Filtreler" sheet would otherwise show as raw guids: the id filters (category, team, assignee,
/// department, location) and an id bucket (team, assignee, category, location or department). Keyed by the id's text; an id that no
/// longer resolves is left out and shown as it is.
/// </summary>
public static class ReportExportNames
{
    private const string NameSeparator = " ";

    public static async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        IAppDbContext db, ReportFilter filter, string? bucket, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        async Task AddAsync(Guid? id, Func<Guid, Task<string?>> lookup)
        {
            if (id is { } value && !names.ContainsKey(value.ToString()) && await lookup(value) is { } name)
            {
                names[value.ToString()] = name;
            }
        }

        Task<string?> Category(Guid id) => db.Categories.Where(c => c.Id == id).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken);
        Task<string?> Team(Guid id) => db.Teams.Where(t => t.Id == id).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken);
        Task<string?> User(Guid id) =>
            db.Users.Where(u => u.Id == id).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefaultAsync(cancellationToken);
        Task<string?> Department(Guid id) => db.Departments.Where(d => d.Id == id).Select(d => d.Name).FirstOrDefaultAsync(cancellationToken);
        Task<string?> Location(Guid id) => db.Locations.Where(l => l.Id == id).Select(l => l.Name).FirstOrDefaultAsync(cancellationToken);

        await AddAsync(filter.CategoryId, Category);
        await AddAsync(filter.TeamId, Team);
        await AddAsync(filter.AssigneeId, User);
        await AddAsync(filter.DepartmentId, Department);
        await AddAsync(filter.LocationId, Location);

        // Bucket ids are uuid v7 values unique across tables, so the first table that knows the id names it.
        if (Guid.TryParse(bucket, out var bucketId))
        {
            await AddAsync(bucketId, async id =>
                await Team(id) ?? await User(id) ?? await Category(id) ?? await Location(id) ?? await Department(id));
        }

        return names;
    }
}
