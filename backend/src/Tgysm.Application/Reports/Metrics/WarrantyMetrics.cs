using Tgysm.Domain.Assets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>
/// The figures of the warranty report (spec §7.2), counted over the filtered assets of any status. Dates are whole days in the company
/// zone; assets without a warranty date are in no figure but <see cref="NoWarrantyDate"/>.
/// </summary>
public static class WarrantyMetrics
{
    public const string Expired = "expired";
    public const string Expiring = "expiring";
    public const string NoWarrantyDate = "no_warranty_date";

    internal static IReadOnlyList<IReportMetric> All { get; } = [new ExpiredMetric(), new ExpiringMetric(), new NoWarrantyDateMetric()];

    /// <summary>Warranty ended before today.</summary>
    private sealed class ExpiredMetric : CountMetric
    {
        public override ReportName Report => ReportName.Warranty;

        public override string Key => Expired;

        public override IQueryable<Asset> Rows(IQueryable<Asset> filtered, AssetReportContext context) =>
            filtered.Where(a => a.WarrantyEndsOn < context.Today);
    }

    /// <summary>Warranty ends inside the window: today (or the later <c>From</c>) up to <c>To</c>, both inclusive.</summary>
    private sealed class ExpiringMetric : CountMetric
    {
        public override ReportName Report => ReportName.Warranty;

        public override string Key => Expiring;

        public override IQueryable<Asset> Rows(IQueryable<Asset> filtered, AssetReportContext context) =>
            filtered.Where(a => a.WarrantyEndsOn >= context.WarrantyFrom && a.WarrantyEndsOn <= context.WarrantyTo);
    }

    private sealed class NoWarrantyDateMetric : CountMetric
    {
        public override ReportName Report => ReportName.Warranty;

        public override string Key => NoWarrantyDate;

        public override IQueryable<Asset> Rows(IQueryable<Asset> filtered, AssetReportContext context) =>
            filtered.Where(a => a.WarrantyEndsOn == null);
    }
}
