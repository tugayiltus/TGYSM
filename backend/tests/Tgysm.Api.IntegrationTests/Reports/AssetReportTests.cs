using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Reports;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class AssetReportTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private static long CountOf(ReportFigure figure, string key) => Assert.Single(figure.Buckets!, b => b.Key == key).Count;

    private async Task<AssetReportFixture> FixtureAsync() => new(await ReportWorld.CreateAsync(factory));

    [Fact]
    public async Task Counts_per_type_status_and_location_include_retired_assets()
    {
        var fx = await FixtureAsync();
        var hq = await TestData.LocationAsync(factory, "Merkez");
        var depot = await TestData.LocationAsync(factory, "Depo");
        await fx.AssetAsync(hq.Id, AssetType.Laptop);
        await fx.AssetAsync(hq.Id, AssetType.Laptop, AssetStatus.Assigned);
        await fx.AssetAsync(depot.Id, AssetType.Phone, AssetStatus.Retired);
        await fx.AssetAsync(depot.Id, AssetType.Phone, AssetStatus.Lost);

        var result = await fx.RunAsync(ReportName.Assets, fx.World.Admin.Id);

        var byType = Figure(result, AssetMetrics.ByType);
        Assert.Equal(4, byType.Value);
        Assert.Equal((2L, 2L), (CountOf(byType, "laptop"), CountOf(byType, "phone")));
        var byStatus = Figure(result, AssetMetrics.ByStatus);
        Assert.Equal((1L, 1L, 1L, 1L), (CountOf(byStatus, "in_stock"), CountOf(byStatus, "assigned"), CountOf(byStatus, "retired"), CountOf(byStatus, "lost")));
        var byLocation = Figure(result, AssetMetrics.ByLocation);
        Assert.Equal(2, CountOf(byLocation, hq.Id.ToString()));
        Assert.Equal("Depo", Assert.Single(byLocation.Buckets!, b => b.Key == depot.Id.ToString()).Label);
        await fx.AssertFiguresMatchRowsAsync(ReportName.Assets);
    }

    [Fact]
    public async Task Active_assignments_stock_and_maintenance_lost_retired()
    {
        var fx = await FixtureAsync();
        var hq = await TestData.LocationAsync(factory, "Merkez");
        await fx.AssetAsync(hq.Id);
        await fx.AssetAsync(hq.Id);
        await fx.AssetAsync(hq.Id, status: AssetStatus.Assigned);
        await fx.AssetAsync(hq.Id, status: AssetStatus.Maintenance, retainedInMaintenance: true); // keeps its open assignment
        await fx.AssetAsync(hq.Id, status: AssetStatus.Maintenance);
        await fx.AssetAsync(hq.Id, status: AssetStatus.Lost);
        await fx.AssetAsync(hq.Id, status: AssetStatus.Retired);

        var result = await fx.RunAsync(ReportName.Assets, fx.World.Desk.Id);

        Assert.Equal(2, Figure(result, AssetMetrics.ActiveAssignments).Value);
        Assert.Equal(2, Figure(result, AssetMetrics.Stock).Value);
        var mlr = Figure(result, AssetMetrics.MaintenanceLostRetired);
        Assert.Equal(4, mlr.Value);
        Assert.Equal((2L, 1L, 1L), (CountOf(mlr, "maintenance"), CountOf(mlr, "lost"), CountOf(mlr, "retired")));
        Assert.DoesNotContain(mlr.Buckets!, b => b.Key is "in_stock" or "assigned");
        await fx.AssertFiguresMatchRowsAsync(ReportName.Assets);
    }

    [Fact]
    public async Task Filters_narrow_every_figure_and_rows_still_match()
    {
        var fx = await FixtureAsync();
        var hq = await TestData.LocationAsync(factory, "Merkez");
        var depot = await TestData.LocationAsync(factory, "Depo");
        var department = await TestData.DepartmentAsync(factory, "Muhasebe");
        await fx.AssetAsync(hq.Id, AssetType.Laptop, departmentId: department.Id);
        await fx.AssetAsync(hq.Id, AssetType.Phone, departmentId: department.Id);
        await fx.AssetAsync(depot.Id, AssetType.Laptop, departmentId: department.Id);
        await fx.AssetAsync(hq.Id, AssetType.Laptop);
        var filter = new ReportFilter { AssetType = AssetType.Laptop, LocationId = hq.Id, DepartmentId = department.Id };

        var result = await fx.RunAsync(ReportName.Assets, fx.World.Admin.Id, filter);

        Assert.Equal(1, Figure(result, AssetMetrics.ByType).Value);
        Assert.Equal(1, Figure(result, AssetMetrics.Stock).Value);
        await fx.AssertFiguresMatchRowsAsync(ReportName.Assets, filter);
    }

    [Fact]
    public async Task Employee_is_forbidden()
    {
        var fx = await FixtureAsync();

        await Assert.ThrowsAsync<Tgysm.Application.Common.ForbiddenException>(() => fx.RunAsync(ReportName.Assets, fx.World.Employee.Id));
    }
}
