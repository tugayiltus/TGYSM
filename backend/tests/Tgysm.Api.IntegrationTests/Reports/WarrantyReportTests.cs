using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class WarrantyReportTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly DateOnly Today = AssetReportFixture.Today;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static double? ValueOf(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric).Value;

    private async Task<AssetReportFixture> FixtureAsync() => new(await ReportWorld.CreateAsync(factory));

    [Fact]
    public async Task Ending_today_is_expiring_not_expired_and_ending_yesterday_is_expired()
    {
        var fx = await FixtureAsync();
        var hq = await TestData.LocationAsync(factory, "Merkez");
        await fx.AssetAsync(hq.Id, warrantyEndsOn: Today);
        await fx.AssetAsync(hq.Id, warrantyEndsOn: Today.AddDays(-1));

        var result = await fx.RunAsync(ReportName.Warranty, fx.World.Admin.Id);

        Assert.Equal(1, ValueOf(result, WarrantyMetrics.Expired));
        Assert.Equal(1, ValueOf(result, WarrantyMetrics.Expiring));
        await fx.AssertFiguresMatchRowsAsync(ReportName.Warranty);
    }

    [Fact]
    public async Task Today_is_the_company_day_not_the_utc_day()
    {
        var fx = await FixtureAsync(); // 2026-03-10 22:00 UTC is 2026-03-11 in Istanbul
        var hq = await TestData.LocationAsync(factory, "Merkez");
        await fx.AssetAsync(hq.Id, warrantyEndsOn: new DateOnly(2026, 3, 10));

        var result = await fx.RunAsync(ReportName.Warranty, fx.World.Admin.Id);

        Assert.Equal(1, ValueOf(result, WarrantyMetrics.Expired));
        Assert.Equal(0, ValueOf(result, WarrantyMetrics.Expiring));
    }

    [Fact]
    public async Task Default_window_is_90_days_and_a_filter_range_replaces_it()
    {
        var fx = await FixtureAsync();
        var hq = await TestData.LocationAsync(factory, "Merkez");
        await fx.AssetAsync(hq.Id, warrantyEndsOn: Today.AddDays(ReportBuckets.DefaultWarrantyWindowDays));
        await fx.AssetAsync(hq.Id, warrantyEndsOn: Today.AddDays(ReportBuckets.DefaultWarrantyWindowDays + 1));
        await fx.AssetAsync(hq.Id, warrantyEndsOn: Today.AddDays(10));

        var byDefault = await fx.RunAsync(ReportName.Warranty, fx.World.Desk.Id);
        var narrow = await fx.RunAsync(ReportName.Warranty, fx.World.Desk.Id, new ReportFilter { To = Today.AddDays(10) });
        var later = await fx.RunAsync(ReportName.Warranty, fx.World.Desk.Id, new ReportFilter { From = Today.AddDays(11), To = Today.AddDays(200) });

        Assert.Equal(2, ValueOf(byDefault, WarrantyMetrics.Expiring));
        Assert.Equal(1, ValueOf(narrow, WarrantyMetrics.Expiring));
        Assert.Equal(2, ValueOf(later, WarrantyMetrics.Expiring));
        await fx.AssertFiguresMatchRowsAsync(ReportName.Warranty, new ReportFilter { To = Today.AddDays(10) });
    }

    [Fact]
    public async Task Assets_without_a_warranty_date_are_only_counted_as_no_warranty_date()
    {
        var fx = await FixtureAsync();
        var hq = await TestData.LocationAsync(factory, "Merkez");
        await fx.AssetAsync(hq.Id);
        await fx.AssetAsync(hq.Id);
        await fx.AssetAsync(hq.Id, warrantyEndsOn: Today.AddDays(5));

        var result = await fx.RunAsync(ReportName.Warranty, fx.World.Admin.Id);

        Assert.Equal(2, ValueOf(result, WarrantyMetrics.NoWarrantyDate));
        Assert.Equal(1, ValueOf(result, WarrantyMetrics.Expiring));
        Assert.Equal(0, ValueOf(result, WarrantyMetrics.Expired));
    }

    [Fact]
    public async Task Employee_is_forbidden()
    {
        var fx = await FixtureAsync();

        await Assert.ThrowsAsync<Tgysm.Application.Common.ForbiddenException>(() => fx.RunAsync(ReportName.Warranty, fx.World.Employee.Id));
    }
}
