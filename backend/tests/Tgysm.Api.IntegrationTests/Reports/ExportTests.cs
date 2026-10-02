using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;
using Tgysm.Infrastructure.Reports;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class ExportTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ReportsPath = "/api/v1/reports";
    private const string March = "from=2026-03-01&to=2026-03-31";
    private const string RecordsSheet = "Kayıtlar";
    private const string FiltersSheet = "Filtreler";
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int BulkSeed = ReportExportLimits.SyncMaxRows;

    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly DateTimeOffset Now = AssetReportFixture.Now;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class CollectingAudit : IAuditLog
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null, object? newValues = null, string? reason = null, Guid? actorUserId = null)
        {
        }
    }

    private async Task<(ReportFixture Tickets, Ticket Foreign)> SeedAsync()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var fx = new ReportFixture(world);
        var assets = new AssetReportFixture(world);

        var done = await fx.IncidentAsync(T0, t => t.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0.AddHours(3)));
        await fx.SlaAsync(done, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, TimeSpan.FromMinutes(30), TimeSpan.Zero, SlaResult.Met));
        var late = await fx.IncidentAsync(T0.AddDays(1), t => t.Complete("gec", TicketOutcome.Resolved, false, false, T0.AddDays(3)));
        await fx.SlaAsync(late, SlaMetric.Resolution, T0.AddDays(1), ReportFixture.StopAfter(T0.AddDays(1), TimeSpan.FromHours(5), TimeSpan.FromMinutes(20), SlaResult.Breached));
        await fx.IncidentAsync(T0.AddDays(3), t => t.Hold(HoldReason.Vendor, null, T0.AddDays(3).AddMinutes(5)));
        await fx.IncidentAsync(T0.AddDays(-5));
        var foreign = await fx.IncidentAsync(T0, teamId: world.FieldTeam.Id);

        var hq = await TestData.LocationAsync(factory, "Merkez");
        await assets.AssetAsync(hq.Id, AssetType.Laptop, warrantyEndsOn: new DateOnly(2026, 3, 20));
        await assets.AssetAsync(hq.Id, AssetType.Laptop, AssetStatus.Assigned);
        await assets.AssetAsync(hq.Id, AssetType.Phone, AssetStatus.Retired);
        return (fx, foreign);
    }

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    private static int RecordCount(XLWorkbook workbook) => workbook.Worksheet(RecordsSheet).LastRowUsed()!.RowNumber() - 1;

    private static async Task<long> DrilldownTotalAsync(ApiClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await client.ReadJsonAsync<JsonElement>(response);
        var rows = body.GetProperty(body.GetProperty("kind").GetString()!);
        return rows.GetProperty("totalCount").GetInt64();
    }

    private static async Task<byte[]> ExportBytesAsync(ApiClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XlsxType, response.Content.Headers.ContentType!.MediaType);
        return await response.Content.ReadAsByteArrayAsync(Ct);
    }

    [Theory]
    [InlineData("incidents", MetricKeys.SlaCompliance, "")] // ratio numerator
    [InlineData("incidents", IncidentMetrics.OpenBacklog, "&bucket=8%2B")] // distribution bucket
    [InlineData("assets", AssetMetrics.ByType, "")] // asset rows
    [InlineData("incidents", MetricKeys.SlaCompliance, "&part=denominator")] // ratio denominator
    public async Task Export_row_count_equals_the_drilldown_total(string report, string metric, string extra)
    {
        await SeedAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");
        var query = $"metric={metric}&{March}{extra}";

        var total = await DrilldownTotalAsync(admin, $"{ReportsPath}/{report}/drilldown?{query}&pageSize=100");
        using var workbook = Open(await ExportBytesAsync(admin, $"{ReportsPath}/{report}/export?{query}"));

        Assert.True(total > 0, "the representative export must not be empty");
        Assert.Equal(total, RecordCount(workbook));
    }

    [Fact]
    public async Task Open_backlog_bucket_with_rows_exports_those_rows()
    {
        await SeedAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");
        using var figures = await admin.GetAsync($"{ReportsPath}/incidents?{March}");
        var body = await admin.ReadJsonAsync<JsonElement>(figures);
        var backlog = body.GetProperty("figures").EnumerateArray().Single(f => f.GetProperty("metric").GetString() == IncidentMetrics.OpenBacklog);
        var bucket = backlog.GetProperty("buckets").EnumerateArray().First(b => b.GetProperty("count").GetInt64() > 0);
        var key = Uri.EscapeDataString(bucket.GetProperty("key").GetString()!);

        var total = await DrilldownTotalAsync(admin, $"{ReportsPath}/incidents/drilldown?metric={IncidentMetrics.OpenBacklog}&{March}&bucket={key}");
        using var workbook = Open(await ExportBytesAsync(admin, $"{ReportsPath}/incidents/export?metric={IncidentMetrics.OpenBacklog}&{March}&bucket={key}"));

        Assert.Equal(bucket.GetProperty("count").GetInt64(), total);
        Assert.Equal(total, RecordCount(workbook));
    }

    [Fact]
    public async Task Filters_sheet_lists_report_metric_filters_scope_and_row_count_and_header_is_frozen_and_filtered()
    {
        await SeedAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");

        using var response = await admin.GetAsync($"{ReportsPath}/incidents/export?metric=volume&{March}&priority=p2&status=open");
        Assert.Matches(@"^tgysm-incidents-volume-\d{12}\.xlsx$", response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName);
        using var workbook = Open(await response.Content.ReadAsByteArrayAsync(Ct));

        var records = workbook.Worksheet(RecordsSheet);
        Assert.Equal("Numara", records.Cell(1, 1).GetString());
        Assert.Equal(1, records.SheetView.SplitRow);
        Assert.NotNull(records.AutoFilter);
        Assert.True(records.AutoFilter.IsEnabled);

        var sheet = workbook.Worksheet(FiltersSheet);
        var pairs = sheet.RowsUsed().Skip(1).ToDictionary(r => r.Cell(1).GetString(), r => r.Cell(2).GetString());
        Assert.Equal("Arızalar", pairs["Rapor"]);
        Assert.Equal("volume", pairs["Ölçü"]);
        Assert.Equal("Pay", pairs["Bölüm"]);
        Assert.Equal("2026-03-01", pairs["Başlangıç tarihi"]);
        Assert.Equal("2026-03-31", pairs["Bitiş tarihi"]);
        Assert.Equal("P2", pairs["Öncelik"]);
        Assert.Equal("Açık", pairs["Durum"]);
        Assert.Equal("Tüm kayıtlar", pairs["Kapsam"]);
        Assert.Equal("0", pairs["Satır sayısı"]);
        Assert.True(pairs.ContainsKey("Oluşturulma zamanı"));
        Assert.False(pairs.ContainsKey("Tür")); // an unset filter is not listed
    }

    [Fact]
    public async Task More_than_the_sync_limit_is_409_and_exactly_the_limit_is_exported()
    {
        var world = await ReportWorld.CreateAsync(factory);
        await using (var db = world.Db())
        {
            db.Tickets.AddRange(Enumerable.Range(1, BulkSeed).Select(i => Ticket.CreateIncident(
                $"INC-2026-{i:D6}", "Konu", "Aciklama", null, null, world.Employee.Id, world.DeskTeam.Id, T0)));
            await db.SaveChangesAsync(Ct);
        }

        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");
        var path = $"{ReportsPath}/incidents/export?metric=volume&{March}";
        using (var workbook = Open(await ExportBytesAsync(admin, path)))
        {
            Assert.Equal(BulkSeed, RecordCount(workbook));
        }

        await using (var db = world.Db())
        {
            db.Tickets.Add(Ticket.CreateIncident($"INC-2026-{BulkSeed + 1:D6}", "Konu", "Aciklama", null, null, world.Employee.Id, world.DeskTeam.Id, T0));
            await db.SaveChangesAsync(Ct);
        }

        using var tooLarge = await admin.GetAsync(path);

        Assert.Equal(HttpStatusCode.Conflict, tooLarge.StatusCode);
        Assert.Equal(ErrorCodes.ReportsExportTooLarge, await ApiClient.ReadCodeAsync(tooLarge));
        await using var check = world.Db();
        Assert.Equal(1, await check.AuditEvents.CountAsync(a => a.Action == AuditActions.ReportExported, Ct)); // the refused one wrote none
    }

    [Fact]
    public async Task It_staff_export_never_lists_tickets_of_other_teams()
    {
        var (fx, foreign) = await SeedAsync();
        var query = new ExportReportQuery(ReportName.Incidents, MetricKeys.Volume, null, ReportDrillPart.Numerator, new ReportFilter { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 31) });

        var admin = await ExportNumbersAsync(fx, fx.World.Admin.Id, query);
        var desk = await ExportNumbersAsync(fx, fx.World.Desk.Id, query);

        Assert.Contains(foreign.Number, admin);
        Assert.DoesNotContain(foreign.Number, desk);
        Assert.NotEmpty(desk);
    }

    [Fact]
    public async Task Employee_is_403()
    {
        await SeedAsync();
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "worker@example.com");

        using var response = await employee.GetAsync($"{ReportsPath}/incidents/export?metric=volume");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Bad_metric_bucket_or_filter_is_400()
    {
        await SeedAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");

        using var metric = await admin.GetAsync($"{ReportsPath}/incidents/export?metric=nope");
        using var bucket = await admin.GetAsync($"{ReportsPath}/incidents/export?metric=volume&bucket=nope");
        using var filter = await admin.GetAsync($"{ReportsPath}/incidents/export?metric=volume&priority=nope");

        Assert.Equal(HttpStatusCode.BadRequest, metric.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bucket.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, filter.StatusCode);
    }

    [Fact]
    public async Task Export_is_audited_with_the_question_and_without_row_data()
    {
        var (fx, foreign) = await SeedAsync();
        var (admin, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");

        _ = await ExportBytesAsync(admin, $"{ReportsPath}/incidents/export?metric=volume&{March}&priority=p3");

        await using var db = fx.World.Db();
        var audit = await db.AuditEvents.SingleAsync(a => a.Action == AuditActions.ReportExported, Ct);
        Assert.Equal(AuditEntityTypes.Report, audit.EntityType);
        Assert.Equal(user.Id, audit.ActorUserId);
        using var values = JsonDocument.Parse(audit.NewValues!);
        Assert.Equal("incidents", values.RootElement.GetProperty("report").GetString());
        Assert.Equal("volume", values.RootElement.GetProperty("metric").GetString());
        Assert.Contains("priority=p3", values.RootElement.GetProperty("filter").GetString());
        Assert.DoesNotContain(foreign.Number, audit.NewValues!);
        Assert.DoesNotContain("INC-", audit.NewValues!);
        Assert.DoesNotContain("Konu", audit.NewValues!);
    }

    private async Task<HashSet<string>> ExportNumbersAsync(ReportFixture fx, Guid userId, ExportReportQuery query)
    {
        await using var db = fx.World.Db();
        var scope = new ReportScope(db, new FixedActor(userId));
        var rows = new ReportRowSource(scope, new TicketReportSource(db, new TestClock()), db, new FakeTimeProvider(Now), new TestClock());
        var handler = new ExportReportHandler(
            scope, rows, new ExcelReportWriter(new TestClock(), new FakeTimeProvider(Now)), new CollectingAudit(), db, new FakeTimeProvider(Now), new TestClock());
        var file = await handler.HandleAsync(query, Ct);
        using var stream = new MemoryStream();
        await file.WriteAsync(stream, Ct);
        using var workbook = new XLWorkbook(new MemoryStream(stream.ToArray()));
        return [.. workbook.Worksheet(RecordsSheet).RowsUsed().Skip(1).Select(r => r.Cell(1).GetString())];
    }
}
