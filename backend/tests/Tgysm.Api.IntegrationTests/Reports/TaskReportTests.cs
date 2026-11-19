using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class TaskReportTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ReportsPath = "/api/v1/reports/tasks";
    private const string March = "from=2026-03-01&to=2026-03-31";
    private const string RecordsSheet = "Kayıtlar";
    private const double SecondsPerHour = 3600;

    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly ReportFilter MarchFilter = new() { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 31) };
    private static readonly string[] ExpectedHeaders = ["Numara", "Başlık", "Durum", "Öncelik", "Ekip", "Atanan", "Son tarih", "Oluşturulma", "Tamamlanma"];

    // The test clock reads 2026-03-10 22:00 UTC, which is already 2026-03-11 in Istanbul.
    private static readonly DateOnly YesterdayInCompany = new(2026, 3, 10);
    private static readonly DateOnly TodayInCompany = new(2026, 3, 11);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private static long CountOf(ReportFigure figure, string key) => Assert.Single(figure.Buckets!, b => b.Key == key).Count;

    /// <summary>
    /// Desk team: two overdue open tasks (one running), one due today, one cancelled past due, one completed after 4 h and one after 8 h.
    /// Field team: one overdue open task.
    /// </summary>
    private async Task<TaskReportFixture> SeededAsync()
    {
        var fx = new TaskReportFixture(await ReportWorld.CreateAsync(factory));
        await fx.TaskAsync(TaskEnding.Open, T0, YesterdayInCompany);
        await fx.TaskAsync(TaskEnding.InProgress, T0.AddDays(-5), new DateOnly(2026, 3, 5));
        await fx.TaskAsync(TaskEnding.Open, T0, TodayInCompany);
        await fx.TaskAsync(TaskEnding.Cancelled, T0, new DateOnly(2026, 3, 1));
        await fx.TaskAsync(TaskEnding.Completed, T0, new DateOnly(2026, 3, 1), TimeSpan.FromHours(4));
        await fx.TaskAsync(TaskEnding.Completed, T0.AddDays(1), null, TimeSpan.FromHours(8));
        await fx.TaskAsync(TaskEnding.Open, T0, YesterdayInCompany, teamId: fx.World.FieldTeam.Id);
        return fx;
    }

    [Fact]
    public async Task Overdue_counts_open_tasks_past_due_in_company_time_and_ignores_the_period()
    {
        var fx = await SeededAsync();

        var march = await fx.RunAsync(ReportName.Tasks, fx.World.Desk.Id, MarchFilter);
        var elsewhere = await fx.RunAsync(ReportName.Tasks, fx.World.Desk.Id, new ReportFilter { From = new DateOnly(2025, 1, 1), To = new DateOnly(2025, 1, 31) });

        Assert.Equal(2, Figure(march, TaskMetrics.Overdue).Value);
        Assert.Equal(2, Figure(elsewhere, TaskMetrics.Overdue).Value);
    }

    [Fact]
    public async Task Status_buckets_count_tasks_created_in_the_period()
    {
        var fx = await SeededAsync();

        var result = await fx.RunAsync(ReportName.Tasks, fx.World.Desk.Id, MarchFilter);

        var byStatus = Figure(result, TaskMetrics.ByStatus);
        Assert.Equal(["open", "in_progress", "on_hold", "completed", "cancelled"], byStatus.Buckets!.Select(b => b.Key));
        Assert.Equal((2L, 1L, 0L, 2L, 1L), (CountOf(byStatus, "open"), CountOf(byStatus, "in_progress"), CountOf(byStatus, "on_hold"), CountOf(byStatus, "completed"), CountOf(byStatus, "cancelled")));
        Assert.Equal(6, byStatus.Value);
    }

    [Fact]
    public async Task Completion_time_is_the_median_and_average_of_completed_tasks()
    {
        var fx = await SeededAsync();
        await fx.TaskAsync(TaskEnding.Completed, T0.AddDays(-3), null, TimeSpan.FromHours(30)); // an outlier moves the average, not the median

        var result = await fx.RunAsync(ReportName.Tasks, fx.World.Admin.Id, MarchFilter);

        var time = Figure(result, TaskMetrics.CompletionTime);
        Assert.Equal(8 * SecondsPerHour, time.Value);
        Assert.Equal(3, time.Denominator);
        Assert.Equal((long)(14 * SecondsPerHour), time.Numerator);
        var even = Figure(await fx.RunAsync(ReportName.Tasks, fx.World.Desk.Id, MarchFilter with { To = new DateOnly(2026, 3, 10) }), TaskMetrics.CompletionTime);
        Assert.Equal((17 * SecondsPerHour, 2L), (even.Value, even.Denominator)); // the 4 h and the 30 h tasks closed by the 10th: the median is their mean
    }

    [Fact]
    public async Task By_team_lists_teams_with_names_and_staff_of_another_team_sees_only_their_own()
    {
        var fx = await SeededAsync();

        var admin = Figure(await fx.RunAsync(ReportName.Tasks, fx.World.Admin.Id, MarchFilter), TaskMetrics.ByTeam);
        var field = Figure(await fx.RunAsync(ReportName.Tasks, fx.World.Field.Id, MarchFilter), TaskMetrics.ByTeam);

        Assert.Equal(7, admin.Value);
        Assert.Equal([fx.World.DeskTeam.Name, fx.World.FieldTeam.Name], admin.Buckets!.Select(b => b.Label));
        Assert.Equal([6L, 1L], admin.Buckets!.Select(b => b.Count));
        Assert.Equal(fx.World.DeskTeam.Id.ToString(), admin.Buckets![0].Key);
        Assert.Equal(1, field.Value);
    }

    [Fact]
    public async Task Staff_of_another_team_sees_zeros_and_the_team_filter_narrows_for_admins()
    {
        var fx = new TaskReportFixture(await ReportWorld.CreateAsync(factory));
        await fx.TaskAsync(TaskEnding.Open, T0, YesterdayInCompany);
        await fx.TaskAsync(TaskEnding.Completed, T0, null, TimeSpan.FromHours(2));

        var foreign = await fx.RunAsync(ReportName.Tasks, fx.World.Field.Id, MarchFilter);
        var filtered = await fx.RunAsync(ReportName.Tasks, fx.World.Admin.Id, MarchFilter with { TeamId = fx.World.FieldTeam.Id });

        foreach (var result in new[] { foreign, filtered })
        {
            Assert.Equal(0, Figure(result, TaskMetrics.Overdue).Value);
            Assert.Equal(0, Figure(result, TaskMetrics.ByStatus).Value);
            Assert.Null(Figure(result, TaskMetrics.CompletionTime).Value);
            Assert.Equal(0, Figure(result, TaskMetrics.ByTeam).Value);
        }

        Assert.Equal(GetReportHandler.TeamsScope, foreign.ScopeLabel);
    }

    [Fact]
    public async Task Employee_is_forbidden_by_handler_and_endpoint()
    {
        var fx = await SeededAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => fx.RunAsync(ReportName.Tasks, fx.World.Employee.Id));

        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "visitor@example.com");
        using var response = await employee.GetAsync($"{ReportsPath}?{March}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Overdue_drilldown_lists_the_two_tasks_and_the_export_equals_those_rows()
    {
        // The endpoint runs on the real clock, so every due date here lies safely in the past and none is "today".
        var fx = new TaskReportFixture(await ReportWorld.CreateAsync(factory));
        await fx.TaskAsync(TaskEnding.Open, T0, YesterdayInCompany);
        await fx.TaskAsync(TaskEnding.InProgress, T0.AddDays(-5), new DateOnly(2026, 3, 5));
        await fx.TaskAsync(TaskEnding.Completed, T0, new DateOnly(2026, 3, 1), TimeSpan.FromHours(4));
        await fx.TaskAsync(TaskEnding.Open, T0);
        await fx.TaskAsync(TaskEnding.Open, T0, YesterdayInCompany, teamId: fx.World.FieldTeam.Id);
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");
        var query = $"metric={TaskMetrics.Overdue}&teamId={fx.World.DeskTeam.Id}&{March}";

        using var drill = await admin.GetAsync($"{ReportsPath}/drilldown?{query}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, drill.StatusCode);
        var body = await admin.ReadJsonAsync<JsonElement>(drill);
        Assert.Equal("tasks", body.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("changes").ValueKind);
        var rows = body.GetProperty("tasks").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(["TSK-2026-000001", "TSK-2026-000002"], rows.Select(r => r.GetProperty("number").GetString()));
        Assert.Equal(["open", "in_progress"], rows.Select(r => r.GetProperty("status").GetString()));
        Assert.All(rows, r => Assert.Equal(fx.World.DeskTeam.Name, r.GetProperty("teamName").GetString()));

        using var export = await admin.GetAsync($"{ReportsPath}/export?{query}");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync(Ct)));
        var sheet = workbook.Worksheet(RecordsSheet);
        Assert.Equal(ExpectedHeaders, sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        Assert.Equal(rows.Count + 1, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal(rows.Select(r => r.GetProperty("number").GetString()), [sheet.Cell(2, 1).GetString(), sheet.Cell(3, 1).GetString()]);
        Assert.Equal(["Açık", "Üzerinde çalışılıyor"], [sheet.Cell(2, 3).GetString(), sheet.Cell(3, 3).GetString()]);
    }

    [Fact]
    public async Task Team_bucket_drilldown_matches_the_bucket_count()
    {
        var fx = await SeededAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");

        using var response = await admin.GetAsync($"{ReportsPath}/drilldown?metric={TaskMetrics.ByTeam}&bucket={fx.World.DeskTeam.Id}&{March}");

        var body = await admin.ReadJsonAsync<JsonElement>(response);
        Assert.Equal(6, body.GetProperty("tasks").GetProperty("totalCount").GetInt64());
    }
}
