using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record ListUsersQuery(string? Search, bool? IsActive, UserRole? Role, string? Sort, int Page, int PageSize);

public static class UserSort
{
    public const string Name = "name";
    public const string NameDescending = "-name";
    public const string Email = "email";
    public const string EmailDescending = "-email";
    public const string CreatedAt = "createdAt";
    public const string CreatedAtDescending = "-createdAt";
}

public sealed class ListUsersHandler(IAppDbContext db)
{
    public Task<PagedResult<UserListItem>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var key = TurkishText.ToSearchKey(query.Search);
            users = users.Where(u => u.SearchName.Contains(key));
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        var rows =
            from user in users
            join department in db.Departments on user.DepartmentId equals (Guid?)department.Id into departments
            from department in departments.DefaultIfEmpty()
            join location in db.Locations on user.LocationId equals (Guid?)location.Id into locations
            from location in locations.DefaultIfEmpty()
            select new
            {
                User = user,
                DepartmentName = department == null ? null : department.Name,
                LocationName = location == null ? null : location.Name,
            };

        rows = query.Sort switch
        {
            UserSort.NameDescending => rows.OrderByDescending(r => r.User.LastName).ThenByDescending(r => r.User.FirstName).ThenBy(r => r.User.Id),
            UserSort.Email => rows.OrderBy(r => r.User.NormalizedEmail).ThenBy(r => r.User.Id),
            UserSort.EmailDescending => rows.OrderByDescending(r => r.User.NormalizedEmail).ThenBy(r => r.User.Id),
            UserSort.CreatedAt => rows.OrderBy(r => r.User.CreatedAt).ThenBy(r => r.User.Id),
            UserSort.CreatedAtDescending => rows.OrderByDescending(r => r.User.CreatedAt).ThenBy(r => r.User.Id),
            _ => rows.OrderBy(r => r.User.LastName).ThenBy(r => r.User.FirstName).ThenBy(r => r.User.Id),
        };

        return rows
            .Select(r => new UserListItem(
                r.User.Id,
                r.User.FirstName,
                r.User.LastName,
                r.User.Email,
                r.User.Role,
                r.User.IsActive,
                r.User.DepartmentId,
                r.DepartmentName,
                r.User.LocationId,
                r.LocationName,
                r.User.Version))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
