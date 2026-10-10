using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Reports;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>
/// Acceptance criterion 5: IT staff never reach a ticket of a team they are not in, through search, report figures, drill-downs or
/// Excel exports. Both teams get the same mix of tickets (completed, breached, cancelled, held, claimed, reassigned, approved,
/// rejected, related); the admin must see every foreign ticket on the same channels, so an empty result is not a silent pass.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CrossTeamLeakTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ReportsPath = "/api/v1/reports";
    private const string March = "from=2026-03-01&to=2026-03-31";
    private const string RecordsSheet = "Kayıtlar";
    private const string DeskEmail = "desk@example.com";
    private const string AdminEmail = "admin@example.com";
    private const string FiguresProperty = "figures";
    private const string MetricProperty = "metric";
    private const string BucketsProperty = "buckets";
    private const string KeyProperty = "key";
    private const string TicketsProperty = "tickets";
    private const string ItemsProperty = "items";
    private const string NumberProperty = "number";
    private const string TotalCountProperty = "totalCount";
    private const int PageSize = PageRequest.MaxPageSize;

    private static readonly DateTimeOffset T0 = ReportFixture.T0;

    /// <summary>The reports whose rows are tickets; asset reports have no team scope.</summary>
    private static readonly ReportName[] TicketReports = [ReportName.Incidents, ReportName.Requests, ReportName.Operations, ReportName.Transfers];

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>The same ticket mix in <paramref name="teamId"/>, worked by <paramref name="staffId"/>; returns the ticket numbers.</summary>
    private static async Task<HashSet<string>> SeedTeamAsync(ReportFixture fx, Guid teamId, Guid staffId)
    {
        var done = await fx.IncidentAsync(T0, t => t.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0.AddHours(3)), teamId);
        await fx.SlaAsync(done, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, TimeSpan.FromMinutes(30), TimeSpan.Zero, SlaResult.Met));
        await fx.SlaAsync(done, SlaMetric.FirstResponse, T0, ReportFixture.StopAfter(T0, TimeSpan.FromMinutes(10), TimeSpan.Zero, SlaResult.Met));
        var late = await fx.IncidentAsync(T0.AddDays(1), t => t.Complete("gec", TicketOutcome.Resolved, false, false, T0.AddDays(3)), teamId);
        await fx.SlaAsync(late, SlaMetric.Resolution, T0.AddDays(1), ReportFixture.StopAfter(T0.AddDays(1), TimeSpan.FromHours(5), TimeSpan.FromMinutes(20), SlaResult.Breached));
        var cancelled = await fx.IncidentAsync(T0.AddDays(2), t => t.Cancel("vazgecildi", T0.AddDays(2).AddHours(1)), teamId);
        var held = await fx.IncidentAsync(T0.AddDays(3), t => t.Hold(HoldReason.Vendor, null, T0.AddDays(3).AddMinutes(5)), teamId);
        var moved = await fx.IncidentAsync(T0.AddDays(4), teamId: teamId);
        await fx.AssignAsync(moved, teamId, staffId, T0.AddDays(4).AddMinutes(5));
        await fx.AssignAsync(moved, teamId, null, T0.AddDays(4).AddMinutes(10));
        var approved = await fx.RequestAsync(T0, requiresApproval: true, teamId: teamId);
        await fx.ApprovalAsync(approved, T0, T0.AddHours(1));
        var rejected = await fx.RequestAsync(T0.AddDays(1), requiresApproval: true, teamId: teamId);
        await fx.ApprovalAsync(rejected, T0.AddDays(1), T0.AddDays(1).AddHours(2), approve: false);
        var delivered = await fx.RequestAsync(T0.AddDays(2), change: t => t.Complete("verildi", null, false, false, T0.AddDays(2).AddHours(4)), teamId: teamId);
        await fx.RelateAsync(moved, approved, T0.AddDays(5));
        return [.. new[] { done, late, cancelled, held, moved, approved, rejected, delivered }.Select(t => t.Number)];
    }

    private async Task<(ApiClient Desk, ApiClient Admin, HashSet<string> Own, HashSet<string> Foreign)> SeedAsync()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var fx = new ReportFixture(world);
        var own = await SeedTeamAsync(fx, world.DeskTeam.Id, world.Desk.Id);
        var foreign = await SeedTeamAsync(fx, world.FieldTeam.Id, world.Field.Id);
        var desk = ApiClient.Create(factory);
        await desk.LoginAsync(DeskEmail);
        var admin = ApiClient.Create(factory);
        await admin.LoginAsync(AdminEmail);
        return (desk, admin, own, foreign);
    }

    /// <summary>Every drill-down request behind every figure of the ticket reports (numerator, denominator and each bucket).</summary>
    private static async Task<List<(string Route, string Query)>> DrillQueriesAsync(ApiClient client)
    {
        var queries = new List<(string Route, string Query)>();
        foreach (var report in TicketReports)
        {
            var route = ReportNames.ToRoute(report);
            using var response = await client.GetAsync($"{ReportsPath}/{route}?{March}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var figures = (await client.ReadJsonAsync<JsonElement>(response)).GetProperty(FiguresProperty).EnumerateArray().ToList();
            foreach (var metric in ReportCatalog.Get(report).OfType<ReportMetric>())
            {
                var query = $"{March}&metric={metric.Key}";
                queries.Add((route, query));
                if (metric.Shape == MetricShape.Ratio)
                {
                    queries.Add((route, $"{query}&part=denominator"));
                }

                // Only distributions drill into their buckets; a duration's buckets are its calendar and SLA parts.
                var figure = figures.Single(f => f.GetProperty(MetricProperty).GetString() == metric.Key);
                if (metric.Shape == MetricShape.Distribution)
                {
                    var buckets = figure.GetProperty(BucketsProperty);
                    queries.AddRange(buckets.EnumerateArray().Select(b => (route, $"{query}&bucket={Uri.EscapeDataString(b.GetProperty(KeyProperty).GetString()!)}")));
                }
            }
        }

        return queries;
    }

    private static async Task AssertOkAsync(HttpResponseMessage response, string route, string query) =>
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"{route}?{query}: {response.StatusCode} {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

    private static async Task<HashSet<string>> DrilldownNumbersAsync(ApiClient client)
    {
        var numbers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (route, query) in await DrillQueriesAsync(client))
        {
            using var response = await client.GetAsync($"{ReportsPath}/{route}/drilldown?{query}&pageSize={PageSize}");
            await AssertOkAsync(response, route, query);
            var tickets = (await client.ReadJsonAsync<JsonElement>(response)).GetProperty(TicketsProperty);
            var items = tickets.GetProperty(ItemsProperty).EnumerateArray().Select(i => i.GetProperty(NumberProperty).GetString()!).ToList();
            Assert.Equal(tickets.GetProperty(TotalCountProperty).GetInt64(), items.Count);
            numbers.UnionWith(items);
        }

        return numbers;
    }

    private static async Task<HashSet<string>> ExportCellsAsync(ApiClient client)
    {
        var cells = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (route, query) in await DrillQueriesAsync(client))
        {
            using var response = await client.GetAsync($"{ReportsPath}/{route}/export?{query}");
            await AssertOkAsync(response, route, query);
            await using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            using var workbook = new XLWorkbook(stream);
            cells.UnionWith(workbook.Worksheet(RecordsSheet).CellsUsed().Select(c => c.GetString()));
        }

        return cells;
    }

    private static async Task<long> SearchCountAsync(ApiClient client, string search)
    {
        using var response = await client.GetAsync($"{TicketWorld.TicketsPath}?search={Uri.EscapeDataString(search)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await client.ReadJsonAsync<JsonElement>(response)).GetProperty(TotalCountProperty).GetInt64();
    }

    [Fact]
    public async Task Drilldowns_of_every_ticket_figure_never_list_a_foreign_team_ticket()
    {
        var (desk, admin, own, foreign) = await SeedAsync();

        var adminNumbers = await DrilldownNumbersAsync(admin);
        var deskNumbers = await DrilldownNumbersAsync(desk);

        Assert.Superset(foreign, adminNumbers);
        Assert.Superset(own, deskNumbers);
        Assert.Empty(deskNumbers.Intersect(foreign));
    }

    [Fact]
    public async Task Excel_exports_of_every_ticket_figure_never_contain_a_foreign_team_ticket()
    {
        var (desk, admin, own, foreign) = await SeedAsync();

        var adminCells = await ExportCellsAsync(admin);
        var deskCells = await ExportCellsAsync(desk);

        Assert.Superset(foreign, adminCells);
        Assert.Superset(own, deskCells);
        Assert.Empty(deskCells.Intersect(foreign));
    }

    [Fact]
    public async Task Ticket_search_and_detail_never_reach_a_foreign_team_ticket()
    {
        var (desk, admin, own, foreign) = await SeedAsync();

        foreach (var number in foreign)
        {
            Assert.Equal(1, await SearchCountAsync(admin, number));
            Assert.Equal(0, await SearchCountAsync(desk, number));
            using var detail = await desk.GetAsync($"{TicketWorld.TicketsPath}/{number}");
            Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        }

        Assert.Equal(1, await SearchCountAsync(desk, own.First()));
    }
}
