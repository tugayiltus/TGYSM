using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Organization.Departments;

internal static class DepartmentQueries
{
    public const string NameTakenMessage = "Another department uses this name.";

    /// <summary>Ordered by name; ordering sits inside the query because the record projection cannot be sorted by EF.</summary>
    public static IQueryable<DepartmentItem> ToItems(this IQueryable<Department> departments, IAppDbContext db) =>
        from department in departments
        join approver in db.Users on department.ApproverUserId equals (Guid?)approver.Id into approvers
        from approver in approvers.DefaultIfEmpty()
        orderby department.Name, department.Id
        select new DepartmentItem(
            department.Id,
            department.Name,
            department.IsActive,
            department.ApproverUserId,
            approver == null ? null : approver.FirstName + " " + approver.LastName,
            department.Version);

    public static Task<DepartmentItem> LoadItemAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Departments.AsNoTracking().Where(d => d.Id == id).ToItems(db).SingleAsync(cancellationToken);

    public static async Task EnsureNameFreeAsync(IAppDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.Departments.AnyAsync(d => d.NormalizedName == key && d.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.DepartmentNameTaken, NameTakenMessage);
        }
    }

    public static async Task EnsureApproverAsync(IAppDbContext db, Guid? approverUserId, CancellationToken cancellationToken)
    {
        if (approverUserId is { } id
            && !await db.Users.AnyAsync(u => u.Id == id && u.IsActive && u.Role == UserRole.Admin, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ApproverInvalid, "The approver must be an active admin.");
        }
    }

    public static IRuleBuilderOptions<T, string> ValidDepartmentName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Department.NameMaxLength);
}
