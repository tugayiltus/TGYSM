using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class ChangeReportTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ReportsPath = "/api/v1/reports/changes";
    private const string March = "from=2026-03-01&to=2026-03-31";
    private const string RecordsSheet = "Kayıtlar";

    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly ReportFilter MarchFilter = new() { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 31) };
    private static readonly string[] ExpectedHeaders = ["Numara", "Başlık", "Durum", "Sonuç", "Risk", "Ekip", "Pencere başlangıcı", "Fiili bitiş"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private static long CountOf(ReportFigure figure, string key) => Assert.Single(figure.Buckets!, b => b.Key == key).Count;

    /// <summary>March: 2 successful, 1 failed and 1 cancelled closed in it, 1 successful closed in February, 1 open; one success was created in February.</summary>
    private async Task<ChangeReportFixture> SeededAsync()
    {
        var fx = new ChangeReportFixture(await ReportWorld.CreateAsync(factory));
        await fx.ChangeAsync(ChangeEnding.Successful, T0, risk: Level.Low);
        await fx.ChangeAsync(ChangeEnding.Successful, new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero), Level.Medium);
        await fx.ChangeAsync(ChangeEnding.Failed, T0.AddDays(1), risk: Level.High);
        await fx.ChangeAsync(ChangeEnding.Cancelled, T0.AddDays(2), risk: Level.Medium);
        await fx.ChangeAsync(ChangeEnding.Successful, new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 11, 9, 0, 0, TimeSpan.Zero));
        await fx.ChangeAsync(ChangeEnding.Open, T0.AddDays(3), risk: Level.High);
        return fx;
    }

    [Fact]
    public async Task Outcomes_success_rate_risk_and_cancelled_follow_their_own_dates()
    {
        var fx = await SeededAsync();

        var result = await fx.RunAsync(ReportName.Changes, fx.World.Admin.Id, MarchFilter);

        var byOutcome = Figure(result, ChangeMetrics.ByOutcome);
        Assert.Equal(3, byOutcome.Value);
        Assert.Equal(["successful", "rolled_back", "failed"], byOutcome.Buckets!.Select(b => b.Key));
        Assert.Equal((2L, 0L, 1L), (CountOf(byOutcome, "successful"), CountOf(byOutcome, "rolled_back"), CountOf(byOutcome, "failed")));
        var rate = Figure(result, ChangeMetrics.SuccessRate);
        Assert.Equal((2L, 3L), (rate.Numerator, rate.Denominator));
        Assert.Equal(2d / 3, rate.Value!.Value, 6);
        Assert.Equal(1, Figure(result, ChangeMetrics.Cancelled).Value);
        var byRisk = Figure(result, ChangeMetrics.ByRisk);
        Assert.Equal(4, byRisk.Value); // created in March: the change created in February is not counted
        Assert.Equal((1L, 1L, 2L), (CountOf(byRisk, "low"), CountOf(byRisk, "medium"), CountOf(byRisk, "high")));
    }

    [Fact]
    public async Task A_rolled_back_change_counts_as_closed_but_not_successful()
    {
        var fx = await SeededAsync();
        await fx.ChangeAsync(ChangeEnding.RolledBack, T0.AddDays(4));

        var result = await fx.RunAsync(ReportName.Changes, fx.World.Admin.Id, MarchFilter);

        Assert.Equal(1, CountOf(Figure(result, ChangeMetrics.ByOutcome), "rolled_back"));
        var rate = Figure(result, ChangeMetrics.SuccessRate);
        Assert.Equal((2L, 4L), (rate.Numerator, rate.Denominator));
    }

    [Fact]
    public async Task Staff_of_another_team_sees_zeros_and_the_team_filter_narrows_for_admins()
    {
        var fx = await SeededAsync();

        var foreign = await fx.RunAsync(ReportName.Changes, fx.World.Field.Id, MarchFilter);
        var filtered = await fx.RunAsync(ReportName.Changes, fx.World.Admin.Id, MarchFilter with { TeamId = fx.World.FieldTeam.Id });
        var own = await fx.RunAsync(ReportName.Changes, fx.World.Desk.Id, MarchFilter);

        foreach (var result in new[] { foreign, filtered })
        {
            Assert.Equal(0, Figure(result, ChangeMetrics.ByOutcome).Value);
            Assert.Null(Figure(result, ChangeMetrics.SuccessRate).Value);
            Assert.Equal(0, Figure(result, ChangeMetrics.Cancelled).Value);
            Assert.Equal(0, Figure(result, ChangeMetrics.ByRisk).Value);
        }

        Assert.Equal(GetReportHandler.TeamsScope, foreign.ScopeLabel);
        Assert.Equal(3, Figure(own, ChangeMetrics.ByOutcome).Value);
    }

    [Fact]
    public async Task Employee_is_forbidden_by_handler_and_endpoint()
    {
        var fx = await SeededAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => fx.RunAsync(ReportName.Changes, fx.World.Employee.Id));

        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "visitor@example.com");
        using var response = await employee.GetAsync($"{ReportsPath}?{March}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Failed_bucket_drilldown_lists_the_one_row_and_the_export_matches_with_turkish_headers()
    {
        var fx = await SeededAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");
        var query = $"metric={ChangeMetrics.ByOutcome}&bucket=failed&{March}";

        using var drill = await admin.GetAsync($"{ReportsPath}/drilldown?{query}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, drill.StatusCode);
        var body = await admin.ReadJsonAsync<JsonElement>(drill);
        Assert.Equal("changes", body.GetProperty("kind").GetString());
        var row = Assert.Single(body.GetProperty("changes").GetProperty("items").EnumerateArray());
        Assert.Equal("failed", row.GetProperty("outcome").GetString());
        Assert.Equal("closed", row.GetProperty("status").GetString());
        Assert.Equal("high", row.GetProperty("risk").GetString());
        Assert.Equal(fx.World.DeskTeam.Name, row.GetProperty("ownerTeamName").GetString());

        using var export = await admin.GetAsync($"{ReportsPath}/export?{query}");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync(Ct)));
        var sheet = workbook.Worksheet(RecordsSheet);
        Assert.Equal(ExpectedHeaders, sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal(row.GetProperty("number").GetString(), sheet.Cell(2, 1).GetString());
        Assert.Equal("Başarısız", sheet.Cell(2, 4).GetString());
    }

    [Fact]
    public async Task Success_rate_drilldown_parts_match_the_figure()
    {
        await SeededAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");

        foreach (var (part, expected) in new[] { ("numerator", 2L), ("denominator", 3L) })
        {
            using var response = await admin.GetAsync($"{ReportsPath}/drilldown?metric={ChangeMetrics.SuccessRate}&part={part}&{March}");
            var body = await admin.ReadJsonAsync<JsonElement>(response);
            Assert.Equal(expected, body.GetProperty("changes").GetProperty("totalCount").GetInt64());
        }
    }
}
