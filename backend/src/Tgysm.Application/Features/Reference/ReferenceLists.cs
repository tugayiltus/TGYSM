using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Reference;

public sealed record ReferenceLocation(Guid Id, string Path);

public sealed record ReferenceDepartment(Guid Id, string Name);

public sealed record ReferenceUser(Guid Id, string FullName, string Email, string? DepartmentName);

public sealed record SearchReferenceUsersQuery(string? Search);

/// <summary>Active locations with full paths, for pickers of every signed-in user.</summary>
public sealed class ListReferenceLocationsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<ReferenceLocation>> HandleAsync(CancellationToken cancellationToken)
    {
        var paths = await LocationPaths.LoadAsync(db, cancellationToken);
        return [.. paths.Entries.Where(e => e.IsActive).OrderBy(e => e.Path, StringComparer.Create(TurkishText.Culture, ignoreCase: true)).Select(e => new ReferenceLocation(e.Id, e.Path))];
    }
}

/// <summary>Active departments, for pickers of every signed-in user.</summary>
public sealed class ListReferenceDepartmentsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<ReferenceDepartment>> HandleAsync(CancellationToken cancellationToken) =>
        await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.NormalizedName)
            .Select(d => new ReferenceDepartment(d.Id, d.Name)).ToListAsync(cancellationToken);
}

/// <summary>Active users matching a name or email, for handover pickers (IT staff and admins only).</summary>
public sealed class SearchReferenceUsersHandler(IAppDbContext db)
{
    public const int MaxResults = 20;

    public async Task<IReadOnlyList<ReferenceUser>> HandleAsync(SearchReferenceUsersQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var key = TurkishText.ToSearchKey(query.Search);
            users = users.Where(u => u.SearchName.Contains(key));
        }

        return await users.OrderBy(u => u.SearchName).Take(MaxResults)
            .Select(u => new ReferenceUser(
                u.Id,
                u.FirstName + " " + u.LastName,
                u.Email,
                db.Departments.Where(d => d.Id == u.DepartmentId).Select(d => d.Name).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
