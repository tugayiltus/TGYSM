using System.Net;
using Microsoft.Extensions.Time.Testing;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>
/// Every figure of every report equals the total of the drill-down behind it, for admin and for IT staff. The loops run over
/// <see cref="ReportCatalog"/>, so a new metric is covered without touching this class.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportConsistencyTests(ApiFactory factory) : IAsyncLifetime
{
    private const string DrilldownPath = "/api/v1/reports";
    private const int LargePage = PageRequest.MaxPageSize;
    private const int SmallPage = 2;

    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly DateTimeOffset Now = AssetReportFixture.Now;
    private static readonly ReportFilter March = new() { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 31) };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static (ReportRowSource Rows, GetDrilldownHandler Handler) HandlerFor(Tgysm.Infrastructure.Persistence.AppDbContext db, Guid userId)
    {
        var scope = new ReportScope(db, new FixedActor(userId));
        var rows = new ReportRowSource(scope, new TicketReportSource(db, new TestClock()), db, new FakeTimeProvider(Now), new TestClock());
        return (rows, new GetDrilldownHandler(scope, rows));
    }

    private static GetDrilldownQuery Query(
        ReportName report, string metric, string? bucket = null, ReportDrillPart part = ReportDrillPart.Numerator, int page = 1, int pageSize = LargePage) =>
        new(report, metric, bucket, part, March, new PageRequest(page, pageSize));

    private async Task<(ReportFixture Tickets, AssetReportFixture Assets)> SeedAsync()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var fx = new ReportFixture(world);
        var assets = new AssetReportFixture(world);

        var done = await fx.IncidentAsync(T0, t => t.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0.AddHours(3)));
        await fx.SlaAsync(done, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, TimeSpan.FromMinutes(30), TimeSpan.Zero, SlaResult.Met));
        await fx.SlaAsync(done, SlaMetric.FirstResponse, T0, ReportFixture.StopAfter(T0, TimeSpan.FromMinutes(10), TimeSpan.Zero, SlaResult.Met));
        var late = await fx.IncidentAsync(T0.AddDays(1), t => t.Complete("gec", TicketOutcome.Resolved, false, false, T0.AddDays(3)));
        await fx.SlaAsync(late, SlaMetric.Resolution, T0.AddDays(1), ReportFixture.StopAfter(T0.AddDays(1), TimeSpan.FromHours(5), TimeSpan.FromMinutes(20), SlaResult.Breached));
        await fx.IncidentAsync(T0.AddDays(2), t => t.Cancel("vazgecildi", T0.AddDays(2).AddHours(1)));
        await fx.IncidentAsync(T0.AddDays(3), t => t.Hold(HoldReason.Vendor, null, T0.AddDays(3).AddMinutes(5)));
        await fx.IncidentAsync(T0.AddDays(-5));
        await fx.IncidentAsync(T0, teamId: world.FieldTeam.Id);
        var moved = await fx.IncidentAsync(T0.AddDays(4));
        await fx.AssignAsync(moved, world.DeskTeam.Id, world.Desk.Id, T0.AddDays(4).AddMinutes(5));
        await fx.AssignAsync(moved, world.FieldTeam.Id, null, T0.AddDays(4).AddMinutes(10));
        await fx.AssignAsync(moved, world.DeskTeam.Id, null, T0.AddDays(4).AddMinutes(15));
        var request = await fx.RequestAsync(T0, requiresApproval: true);
        await fx.ApprovalAsync(request, T0, T0.AddHours(1));
        var rejected = await fx.RequestAsync(T0.AddDays(1), requiresApproval: true);
        await fx.ApprovalAsync(rejected, T0.AddDays(1), T0.AddDays(1).AddHours(2), approve: false);
        await fx.RequestAsync(T0.AddDays(2), change: t => t.Complete("verildi", null, false, false, T0.AddDays(2).AddHours(4)));
        await fx.RelateAsync(moved, request, T0.AddDays(5));

        var hq = await TestData.LocationAsync(factory, "Merkez");
        var depot = await TestData.LocationAsync(factory, "Depo");
        await assets.AssetAsync(hq.Id, AssetType.Laptop, warrantyEndsOn: new DateOnly(2026, 3, 20));
        await assets.AssetAsync(hq.Id, AssetType.Laptop, AssetStatus.Assigned, new DateOnly(2026, 3, 5));
        await assets.AssetAsync(depot.Id, AssetType.Phone, AssetStatus.Maintenance, new DateOnly(2026, 8, 1));
        await assets.AssetAsync(depot.Id, AssetType.Phone, AssetStatus.Retired);
        await assets.AssetAsync(depot.Id, AssetType.Tablet, AssetStatus.Lost, new DateOnly(2026, 3, 25));
        return (fx, assets);
    }

    private static long TotalOf(DrilldownResult result) => (result.Tickets?.TotalCount ?? result.Assets?.TotalCount)!.Value;

    [Fact]
    public async Task Every_figure_equals_its_drilldown_total_for_admin_and_it_staff()
    {
        var (fx, _) = await SeedAsync();
        var checks = 0;
        var nonEmpty = 0;
        foreach (var userId in new[] { fx.World.Admin.Id, fx.World.Desk.Id })
        {
            foreach (var report in Enum.GetValues<ReportName>())
            {
                await using var db = fx.World.Db();
                var (_, handler) = HandlerFor(db, userId);
                var result = await fx.RunAsync(report, userId, March, Now);
                foreach (var metric in ReportCatalog.Get(report))
                {
                    var figure = Assert.Single(result.Figures, f => f.Metric == metric.Key);
                    async Task<long> TotalAsync(string? bucket = null, ReportDrillPart part = ReportDrillPart.Numerator)
                    {
                        checks++;
                        var total = TotalOf(await handler.HandleAsync(Query(report, metric.Key, bucket, part), Ct));
                        nonEmpty += total > 0 ? 1 : 0;
                        return total;
                    }

                    switch (metric.Shape)
                    {
                        case MetricShape.Count:
                            Assert.Equal((long)figure.Value!.Value, await TotalAsync());
                            break;
                        case MetricShape.Distribution:
                            Assert.Equal((long)figure.Value!.Value, await TotalAsync());
                            foreach (var bucket in figure.Buckets!)
                            {
                                Assert.Equal(bucket.Count, await TotalAsync(bucket.Key));
                            }

                            break;
                        case MetricShape.Duration:
                            Assert.Equal(figure.Denominator, await TotalAsync());
                            break;
                        default:
                            Assert.Equal(figure.Denominator, await TotalAsync(part: ReportDrillPart.Denominator));
                            if (metric.Key != IncidentMetrics.Mttr)
                            {
                                Assert.Equal(figure.Numerator, await TotalAsync());
                            }

                            break;
                    }
                }
            }
        }

        Assert.True(checks > 100, $"only {checks} checks ran");
        Assert.True(nonEmpty > 40, $"only {nonEmpty} drill-downs had rows");
    }

    [Fact]
    public async Task Asset_reports_return_asset_rows_and_ticket_reports_ticket_rows()
    {
        var (fx, _) = await SeedAsync();
        await using var db = fx.World.Db();
        var (_, handler) = HandlerFor(db, fx.World.Admin.Id);

        var assets = await handler.HandleAsync(Query(ReportName.Assets, AssetMetrics.ByType), Ct);
        var tickets = await handler.HandleAsync(Query(ReportName.Incidents, MetricKeys.Volume), Ct);

        Assert.Equal(DrilldownKind.Assets, assets.Kind);
        Assert.Null(assets.Tickets);
        Assert.Equal(5, assets.Assets!.TotalCount);
        Assert.Equal("AST-0001", assets.Assets.Items[0].AssetTag);
        Assert.Equal("Merkez", assets.Assets.Items[0].LocationName);
        Assert.Equal(new DateOnly(2026, 3, 20), assets.Assets.Items[0].WarrantyEndsOn);
        Assert.Equal(DrilldownKind.Tickets, tickets.Kind);
        Assert.Null(tickets.Assets);
        var first = tickets.Tickets!.Items[0];
        Assert.StartsWith("INC-2026-", first.Number);
        Assert.Equal("Masa", first.TeamName);
    }

    [Fact]
    public async Task Staff_drilldown_only_lists_tickets_of_their_teams()
    {
        var (fx, _) = await SeedAsync();
        await using var db = fx.World.Db();
        var (_, admin) = HandlerFor(db, fx.World.Admin.Id);
        var (_, desk) = HandlerFor(db, fx.World.Desk.Id);

        var all = await admin.HandleAsync(Query(ReportName.Incidents, MetricKeys.Volume), Ct);
        var mine = await desk.HandleAsync(Query(ReportName.Incidents, MetricKeys.Volume), Ct);

        Assert.True(all.Tickets!.TotalCount > mine.Tickets!.TotalCount);
        Assert.All(mine.Tickets.Items, row => Assert.Equal("Masa", row.TeamName));
    }

    [Fact]
    public async Task Pages_of_the_transfer_drilldown_are_stable_and_keep_one_row_per_history_row()
    {
        var (fx, _) = await SeedAsync();
        await using var db = fx.World.Db();
        var (_, handler) = HandlerFor(db, fx.World.Admin.Id);

        var whole = await handler.HandleAsync(Query(ReportName.Transfers, TransferMetrics.TransferCount), Ct);
        var paged = new List<string>();
        for (var page = 1; page <= (whole.Tickets!.TotalCount + SmallPage - 1) / SmallPage; page++)
        {
            var slice = await handler.HandleAsync(Query(ReportName.Transfers, TransferMetrics.TransferCount, page: page, pageSize: SmallPage), Ct);
            Assert.Equal(whole.Tickets.TotalCount, slice.Tickets!.TotalCount);
            paged.AddRange(slice.Tickets.Items.Select(r => r.Number));
        }

        Assert.Equal(2, whole.Tickets.TotalCount); // the claim is not a transfer; Desk -> Field -> Desk is two
        Assert.Equal(whole.Tickets.Items.Select(r => r.Number), paged);
        Assert.All(paged, n => Assert.Equal(paged[0], n)); // one ticket, listed once per history row

        var incidents = await handler.HandleAsync(Query(ReportName.Incidents, MetricKeys.Volume), Ct);
        var ordered = incidents.Tickets!.Items.Select(r => (r.CreatedAt, r.Number)).ToList();
        Assert.Equal(ordered.OrderBy(r => r.CreatedAt).ToList(), ordered);
    }

    [Fact]
    public async Task Bad_metric_part_or_bucket_is_400_never_500()
    {
        var (fx, _) = await SeedAsync();
        await using var db = fx.World.Db();
        var (_, handler) = HandlerFor(db, fx.World.Admin.Id);

        await AssertInvalidAsync(handler, Query(ReportName.Incidents, "nope"), "metric", ErrorCodes.ReportsUnknownMetric);
        await AssertInvalidAsync(handler, Query(ReportName.Incidents, MetricKeys.Volume, part: ReportDrillPart.Denominator), "part", ErrorCodes.ReportsInvalidFilter);
        await AssertInvalidAsync(handler, Query(ReportName.Incidents, MetricKeys.Volume, "nope"), "bucket", ErrorCodes.ReportsInvalidFilter);
        await AssertInvalidAsync(handler, Query(ReportName.Incidents, IncidentMetrics.OpenBacklog, "nope"), "bucket", ErrorCodes.ReportsInvalidFilter);
        await AssertInvalidAsync(handler, Query(ReportName.Operations, OperationsMetrics.ByLocation, "not-a-guid"), "bucket", ErrorCodes.ReportsInvalidFilter);
        await AssertInvalidAsync(handler, Query(ReportName.Assets, AssetMetrics.ByType, "nope"), "bucket", ErrorCodes.ReportsInvalidFilter);
        await AssertInvalidAsync(handler, Query(ReportName.Assets, AssetMetrics.ByType, part: ReportDrillPart.Denominator), "part", ErrorCodes.ReportsInvalidFilter);
        await AssertInvalidAsync(handler, Query(ReportName.Incidents, MetricKeys.SlaCompliance, "open"), "bucket", ErrorCodes.ReportsInvalidFilter);
    }

    [Fact]
    public async Task Endpoint_answers_rows_and_maps_bad_input_to_400_and_employees_to_403()
    {
        await SeedAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "worker@example.com");
        const string range = "from=2026-03-01&to=2026-03-31";

        using var ok = await admin.GetAsync($"{DrilldownPath}/incidents/drilldown?metric=volume&{range}&pageSize=3");
        using var unknownMetric = await admin.GetAsync($"{DrilldownPath}/incidents/drilldown?metric=nope");
        using var missingMetric = await admin.GetAsync($"{DrilldownPath}/incidents/drilldown");
        using var badPart = await admin.GetAsync($"{DrilldownPath}/incidents/drilldown?metric=volume&part=denominator");
        using var unknownPart = await admin.GetAsync($"{DrilldownPath}/incidents/drilldown?metric=volume&part=sideways");
        using var badBucket = await admin.GetAsync($"{DrilldownPath}/incidents/drilldown?metric=open_backlog&bucket=nope");
        using var forbidden = await employee.GetAsync($"{DrilldownPath}/incidents/drilldown?metric=volume");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await admin.ReadJsonAsync<System.Text.Json.JsonElement>(ok);
        Assert.Equal("tickets", body.GetProperty("kind").GetString());
        Assert.Equal(3, body.GetProperty("tickets").GetProperty("items").GetArrayLength());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("assets").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, unknownMetric.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingMetric.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badPart.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownPart.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badBucket.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private static async Task AssertInvalidAsync(GetDrilldownHandler handler, GetDrilldownQuery query, string field, string code)
    {
        var failure = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => handler.HandleAsync(query, Ct));
        Assert.Contains(failure.Errors, e => e.PropertyName == field && e.ErrorCode == code);
    }
}
