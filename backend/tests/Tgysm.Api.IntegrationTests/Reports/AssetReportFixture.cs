using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Reports;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Reports;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>Asset seeding and report running for the asset and warranty report tests.</summary>
internal sealed class AssetReportFixture(ReportWorld world)
{
    /// <summary>Wall clock of the runs: 2026-03-10 22:00 UTC is already 2026-03-11 in Istanbul, which pins "today" to the company zone.</summary>
    public static readonly DateTimeOffset Now = new(2026, 3, 10, 22, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 3, 11);

    private int _counter;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReportWorld World { get; } = world;

    /// <summary>Stores an asset in <paramref name="status"/>; assigned assets get an open assignment to the employee.</summary>
    public async Task<Asset> AssetAsync(
        Guid locationId,
        AssetType type = AssetType.Laptop,
        AssetStatus status = AssetStatus.InStock,
        DateOnly? warrantyEndsOn = null,
        Guid? departmentId = null,
        bool retainedInMaintenance = false)
    {
        var initial = status is AssetStatus.Retired ? AssetStatus.Retired : AssetStatus.InStock;
        var model = new AssetCatalogEntry(await TestData.ModelAsync(World.Factory, "Marka", "Model", type), type, "Marka", "Model");
        var (asset, created) = Asset.Create(
            $"AST-{++_counter:D4}", model, null, locationId, departmentId, warrantyEndsOn, null, null, Now, initial, World.Admin.Id);
        var changes = new List<AssetChange>();
        switch (status)
        {
            case AssetStatus.Assigned:
                changes.Add(asset.Assign(World.Employee.Id, World.Admin.Id, null, null, Now));
                break;
            case AssetStatus.Maintenance when retainedInMaintenance:
                var assign = asset.Assign(World.Employee.Id, World.Admin.Id, null, null, Now);
                changes.Add(assign);
                changes.Add(asset.StartMaintenance(assign.OpenedAssignment, true, World.Admin.Id, Now));
                break;
            case AssetStatus.Maintenance:
                changes.Add(asset.StartMaintenance(null, false, World.Admin.Id, Now));
                break;
            case AssetStatus.Lost:
                changes.Add(asset.MarkLost(null, World.Admin.Id, "kayip", Now));
                break;
        }

        await using var db = World.Db();
        db.Assets.Add(asset);
        db.AssetMovements.Add(created);
        foreach (var change in changes)
        {
            db.AssetMovements.Add(change.Movement);
            if (change.OpenedAssignment is { } assignment)
            {
                db.AssetAssignments.Add(assignment);
            }
        }

        await db.SaveChangesAsync(Ct);
        return asset;
    }

    public async Task<ReportResult> RunAsync(ReportName report, Guid userId, ReportFilter? filter = null)
    {
        await using var db = World.Db();
        var handler = new GetReportHandler(
            new ReportScope(db, new FixedActor(userId)), new TicketReportSource(db, new TestClock()), db, new FakeTimeProvider(Now), new TestClock());
        return await handler.HandleAsync(new GetReportQuery(report, filter ?? new ReportFilter()), Ct);
    }

    /// <summary>Checks every metric of <paramref name="report"/>: its figure counts exactly the rows (and bucket rows) it exposes.</summary>
    public async Task AssertFiguresMatchRowsAsync(ReportName report, ReportFilter? filter = null)
    {
        var effective = filter ?? new ReportFilter();
        await using var db = World.Db();
        var context = AssetReportContext.Create(db, effective, Now, new TestClock().Zone);
        var assets = AssetReportSource.Apply(db.Assets, effective);
        var metrics = ReportCatalog.Get(report).Cast<AssetReportMetric>().ToList();
        Assert.NotEmpty(metrics);
        foreach (var metric in metrics)
        {
            var figure = await metric.ComputeAsync(assets, context, Ct);
            var rows = await metric.Rows(assets, context).CountAsync(Ct);
            Assert.Equal(rows, (long)figure.Value!.Value);
            if (metric.Shape == MetricShape.Distribution)
            {
                Assert.Equal(rows, figure.Buckets!.Sum(b => b.Count));
                foreach (var bucket in figure.Buckets!)
                {
                    Assert.Equal(bucket.Count, await metric.BucketRows(assets, context, bucket.Key).CountAsync(Ct));
                }
            }
        }
    }
}
