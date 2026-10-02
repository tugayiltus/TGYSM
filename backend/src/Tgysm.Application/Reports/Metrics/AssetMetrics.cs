using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Assets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>The figures of the asset report (spec §7.2): snapshots of the filtered assets as they are now, no date window.</summary>
public static class AssetMetrics
{
    public const string ByType = "by_type";
    public const string ByStatus = "by_status";
    public const string ByLocation = "by_location";
    public const string ActiveAssignments = "active_assignments";
    public const string Stock = "stock";
    public const string MaintenanceLostRetired = "maintenance_lost_retired";

    private static readonly AssetStatus[] SetAside = [AssetStatus.Maintenance, AssetStatus.Lost, AssetStatus.Retired];

    internal static IReadOnlyList<IReportMetric> All { get; } =
    [
        new EnumMetric<AssetType>(ByType, a => a.Type),
        new EnumMetric<AssetStatus>(ByStatus, a => a.Status),
        new LocationMetric(),
        new ActiveAssignmentsMetric(),
        new StockMetric(),
        new EnumMetric<AssetStatus>(MaintenanceLostRetired, a => a.Status, a => SetAside.Contains(a.Status)),
    ];

    /// <summary>Assets with an open assignment; maintenance with retained responsibility keeps it. An asset has at most one open assignment, so assets equal assignments.</summary>
    private sealed class ActiveAssignmentsMetric : CountMetric
    {
        public override string Key => ActiveAssignments;

        public override IQueryable<Asset> Rows(IQueryable<Asset> filtered, AssetReportContext context)
        {
            var assignments = context.Db.AssetAssignments;
            return filtered.Where(a => assignments.Any(x => x.AssetId == a.Id && x.ReturnedAt == null));
        }
    }

    private sealed class StockMetric : CountMetric
    {
        public override string Key => Stock;

        public override IQueryable<Asset> Rows(IQueryable<Asset> filtered, AssetReportContext context) =>
            filtered.Where(a => a.Status == AssetStatus.InStock);
    }

    /// <summary>Assets by one enum property; the bucket key is the snake_case name, the label stays null (the UI translates it).</summary>
    private sealed class EnumMetric<TEnum>(string key, Expression<Func<Asset, TEnum>> keyOf, Expression<Func<Asset, bool>>? population = null)
        : AssetGroupedMetric<TEnum>
        where TEnum : struct, Enum
    {
        public override string Key => key;

        protected override IQueryable<Asset> Population(IQueryable<Asset> filtered) => population is null ? filtered : filtered.Where(population);

        protected override Expression<Func<Asset, TEnum>> KeyOf() => keyOf;

        protected override string BucketKey(TEnum value) => ReportFilter.EnumName(value);

        protected override TEnum Parse(string bucket) =>
            Enum.GetValues<TEnum>().Cast<TEnum?>().FirstOrDefault(v => ReportFilter.EnumName(v!.Value) == bucket)
                ?? throw new NotSupportedException($"Unknown bucket '{bucket}'.");

        protected override Task<Dictionary<string, string>> LabelsAsync(AssetReportContext context, List<TEnum> keys, CancellationToken cancellationToken) =>
            Task.FromResult(new Dictionary<string, string>());
    }

    private sealed class LocationMetric : AssetGroupedMetric<Guid>
    {
        public override string Key => ByLocation;

        protected override IQueryable<Asset> Population(IQueryable<Asset> filtered) => filtered;

        protected override Expression<Func<Asset, Guid>> KeyOf() => a => a.LocationId;

        protected override string BucketKey(Guid id) => id.ToString();

        protected override Guid Parse(string bucket) =>
            Guid.TryParse(bucket, out var id) ? id : throw new NotSupportedException($"Unknown bucket '{bucket}'.");

        protected override async Task<Dictionary<string, string>> LabelsAsync(AssetReportContext context, List<Guid> keys, CancellationToken cancellationToken) =>
            (await context.Db.Locations.Where(l => keys.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name, cancellationToken))
                .ToDictionary(l => l.Key.ToString(), l => l.Value);
    }
}

/// <summary>A count of assets: <see cref="Rows"/> are the counted assets.</summary>
internal abstract class CountMetric : AssetReportMetric
{
    public override ReportName Report => ReportName.Assets;

    public override MetricShape Shape => MetricShape.Count;

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Asset> filtered, AssetReportContext context, CancellationToken cancellationToken) =>
        new(Key, Shape, await Rows(filtered, context).LongCountAsync(cancellationToken), null, null, null, MetricKeys.UnitAssets);
}

/// <summary>A distribution that groups an asset population by one key; buckets drill down through <see cref="BucketRows"/> with the same predicate.</summary>
internal abstract class AssetGroupedMetric<TKey> : AssetReportMetric
{
    public override ReportName Report => ReportName.Assets;

    public override MetricShape Shape => MetricShape.Distribution;

    protected abstract IQueryable<Asset> Population(IQueryable<Asset> filtered);

    protected abstract Expression<Func<Asset, TKey>> KeyOf();

    protected abstract string BucketKey(TKey key);

    protected abstract TKey Parse(string bucket);

    /// <summary>Display names by bucket key; keys without an entry get a null label.</summary>
    protected abstract Task<Dictionary<string, string>> LabelsAsync(AssetReportContext context, List<TKey> keys, CancellationToken cancellationToken);

    public override IQueryable<Asset> Rows(IQueryable<Asset> filtered, AssetReportContext context) => Population(filtered);

    public override IQueryable<Asset> BucketRows(IQueryable<Asset> filtered, AssetReportContext context, string bucket)
    {
        var key = KeyOf();
        var equal = Expression.Equal(key.Body, Expression.Constant(Parse(bucket), typeof(TKey)));
        return Population(filtered).Where(Expression.Lambda<Func<Asset, bool>>(equal, key.Parameters));
    }

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Asset> filtered, AssetReportContext context, CancellationToken cancellationToken)
    {
        var groups = await Population(filtered)
            .GroupBy(KeyOf())
            .Select(g => new { g.Key, Count = g.LongCount() })
            .ToListAsync(cancellationToken);
        var labels = await LabelsAsync(context, [.. groups.Select(g => g.Key)], cancellationToken);
        var buckets = groups
            .Select(g => (Key: BucketKey(g.Key), g.Count))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ReportBucket(g.Key, labels.GetValueOrDefault(g.Key), g.Count))
            .ToList();
        return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitAssets);
    }
}
