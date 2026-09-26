using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Reference;

public sealed record ReferenceLocation(Guid Id, string Path);

public sealed record ReferenceDepartment(Guid Id, string Name);

public sealed record ReferenceUser(Guid Id, string FullName, string Email, string? DepartmentName);

public sealed record SearchReferenceUsersQuery(string? Search);

public sealed record ReferenceCategory(Guid Id, string Path, Guid? ParentId);

public sealed record ReferenceRequestType(Guid Id, string Name, bool RequiresApproval);

public sealed record ListReferenceCategoriesQuery(TicketKind? Kind);

/// <summary>Active categories (children of inactive parents excluded) that apply to the ticket kind, parents first.</summary>
public sealed class ListReferenceCategoriesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<ReferenceCategory>> HandleAsync(ListReferenceCategoriesQuery query, CancellationToken cancellationToken)
    {
        var all = await db.Categories.AsNoTracking().Where(c => c.IsActive).ToListAsync(cancellationToken);
        var byId = all.ToDictionary(c => c.Id);
        return [.. all
            .Where(c => query.Kind is not { } kind || c.AppliesTo(kind))
            .Where(c => c.ParentId is null || byId.ContainsKey(c.ParentId.Value))
            .Select(c => new ReferenceCategory(c.Id, c.ParentId is { } parent ? $"{byId[parent].Name}{LocationPaths.Separator}{c.Name}" : c.Name, c.ParentId))
            .OrderBy(c => c.Path, StringComparer.Create(TurkishText.Culture, ignoreCase: true))];
    }
}

/// <summary>Active request types.</summary>
public sealed class ListReferenceRequestTypesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<ReferenceRequestType>> HandleAsync(CancellationToken cancellationToken) =>
        await db.RequestTypes.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.NormalizedName)
            .Select(r => new ReferenceRequestType(r.Id, r.Name, r.RequiresApproval)).ToListAsync(cancellationToken);
}

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
