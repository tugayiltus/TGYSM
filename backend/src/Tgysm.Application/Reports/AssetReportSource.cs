using Tgysm.Domain.Assets;

namespace Tgysm.Application.Reports;

/// <summary>Applies the asset-related parts of a <see cref="ReportFilter"/> (no date window: asset reports are snapshots).</summary>
public static class AssetReportSource
{
    public static IQueryable<Asset> Apply(IQueryable<Asset> assets, ReportFilter filter)
    {
        if (filter.AssetType is { } type)
        {
            assets = assets.Where(a => a.Type == type);
        }

        if (filter.AssetStatus is { } status)
        {
            assets = assets.Where(a => a.Status == status);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            assets = assets.Where(a => a.DepartmentId == departmentId);
        }

        if (filter.LocationId is { } locationId)
        {
            assets = assets.Where(a => a.LocationId == locationId);
        }

        return assets;
    }
}
