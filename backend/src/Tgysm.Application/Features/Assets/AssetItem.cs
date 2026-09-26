using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Assets;

namespace Tgysm.Application.Features.Assets;

/// <summary>Asset card with current holder (from the active assignment).</summary>
public sealed record AssetItem(
    Guid Id,
    string AssetTag,
    AssetType Type,
    string Brand,
    string Model,
    string? SerialNumber,
    AssetStatus Status,
    Guid LocationId,
    string LocationName,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? AssignedUserId,
    string? AssignedUserName,
    DateOnly? WarrantyEndsOn,
    DateOnly? PurchasedOn,
    string? Notes,
    DateTimeOffset CreatedAt,
    uint Version);

internal static class AssetQueries
{
    /// <summary>Ordered by normalized tag; ordering sits inside because the record projection cannot be sorted by EF.</summary>
    public static IQueryable<AssetItem> ToItems(this IQueryable<Asset> assets, IAppDbContext db) =>
        from asset in assets
        join location in db.Locations on asset.LocationId equals location.Id
        join department in db.Departments on asset.DepartmentId equals (Guid?)department.Id into departments
        from department in departments.DefaultIfEmpty()
        join assignment in db.AssetAssignments.Where(a => a.ReturnedAt == null) on asset.Id equals assignment.AssetId into assignments
        from assignment in assignments.DefaultIfEmpty()
        join holder in db.Users on (Guid?)assignment.UserId equals (Guid?)holder.Id into holders
        from holder in holders.DefaultIfEmpty()
        orderby asset.NormalizedAssetTag, asset.Id
        select new AssetItem(
            asset.Id,
            asset.AssetTag,
            asset.Type,
            asset.Brand,
            asset.Model,
            asset.SerialNumber,
            asset.Status,
            asset.LocationId,
            location.Name,
            asset.DepartmentId,
            department == null ? null : department.Name,
            assignment == null ? null : assignment.UserId,
            holder == null ? null : holder.FirstName + " " + holder.LastName,
            asset.WarrantyEndsOn,
            asset.PurchasedOn,
            asset.Notes,
            asset.CreatedAt,
            asset.Version);

    public static Task<AssetItem> LoadItemAsync(IAppDbContext db, Guid assetId, CancellationToken cancellationToken) =>
        db.Assets.AsNoTracking().Where(a => a.Id == assetId).ToItems(db).SingleAsync(cancellationToken);
}
