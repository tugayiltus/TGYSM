using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class TransferReportTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly DateTimeOffset Now = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly ReportFilter March10 = new() { From = new DateOnly(2026, 3, 10), To = new DateOnly(2026, 3, 10) };

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private async Task<ReportFixture> FixtureAsync() => new(await ReportWorld.CreateAsync(factory));

    private static async Task<ReportFigure> RunAsync(ReportFixture fx, Guid userId, string metric, ReportFilter? filter = null) =>
        Figure(await fx.RunAsync(ReportName.Transfers, userId, filter ?? March10, Now), metric);

    [Fact]
    public async Task A_claim_within_the_team_is_not_a_transfer()
    {
        var fx = await FixtureAsync();
        var ticket = await fx.IncidentAsync(T0);
        await fx.AssignAsync(ticket, fx.World.DeskTeam.Id, fx.World.Desk.Id, T0.AddMinutes(5)); // claim: same team, no previous assignee

        Assert.Equal(0, (await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.TransferCount)).Value);
        Assert.Equal(0, (await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.Reassignments)).Value);
        var rate = await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.TransferRate);
        Assert.Equal((0L, 1L, 0d), (rate.Numerator!.Value, rate.Denominator!.Value, rate.Value!.Value));
    }

    [Fact]
    public async Task Transfer_count_counts_rows_and_the_rate_counts_tickets()
    {
        var fx = await FixtureAsync();
        var twice = await fx.IncidentAsync(T0);
        await fx.AssignAsync(twice, fx.World.FieldTeam.Id, null, T0.AddMinutes(5));
        await fx.AssignAsync(twice, fx.World.DeskTeam.Id, null, T0.AddMinutes(10));
        var once = await fx.IncidentAsync(T0);
        await fx.AssignAsync(once, fx.World.FieldTeam.Id, null, T0.AddMinutes(5));
        await fx.IncidentAsync(T0);
        var outside = await fx.IncidentAsync(T0);
        await fx.AssignAsync(outside, fx.World.FieldTeam.Id, null, T0.AddDays(30)); // transferred after the window

        Assert.Equal(3, (await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.TransferCount)).Value);
        var rate = await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.TransferRate);
        Assert.Equal(2, rate.Numerator);
        Assert.Equal(4, rate.Denominator);
        Assert.Equal(0.5, rate.Value);
        await fx.AssertFiguresMatchRowsAsync(ReportName.Transfers, March10, Now);
    }

    [Fact]
    public async Task A_reassignment_stays_in_the_team_and_leaves_a_previous_assignee()
    {
        var fx = await FixtureAsync();
        var second = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "desk2@example.com");
        var ticket = await fx.IncidentAsync(T0);
        await fx.AssignAsync(ticket, fx.World.DeskTeam.Id, fx.World.Desk.Id, T0.AddMinutes(5)); // claim
        await fx.AssignAsync(ticket, fx.World.DeskTeam.Id, second.Id, T0.AddMinutes(10)); // reassignment
        await fx.AssignAsync(ticket, fx.World.FieldTeam.Id, null, T0.AddMinutes(15)); // transfer, not a reassignment

        Assert.Equal(1, (await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.Reassignments)).Value);
        Assert.Equal(1, (await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.TransferCount)).Value);
        await fx.AssertFiguresMatchRowsAsync(ReportName.Transfers, March10, Now);
    }

    [Fact]
    public async Task An_incident_to_request_relation_counts_once_in_the_window()
    {
        var fx = await FixtureAsync();
        var incident = await fx.IncidentAsync(T0);
        var request = await fx.RequestAsync(T0);
        await fx.RelateAsync(incident, request, T0.AddMinutes(30));
        var oldIncident = await fx.IncidentAsync(T0.AddDays(-30));
        await fx.RelateAsync(oldIncident, await fx.RequestAsync(T0.AddDays(-30)), T0.AddDays(-30)); // related before the window

        Assert.Equal(1, (await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.IncidentToRequest)).Value);
        await fx.AssertFiguresMatchRowsAsync(ReportName.Transfers, March10, Now);
    }

    [Fact]
    public async Task An_incident_outside_the_viewers_teams_is_not_counted()
    {
        var fx = await FixtureAsync();
        var foreign = await fx.IncidentAsync(T0, teamId: fx.World.FieldTeam.Id, requesterId: fx.World.Employee.Id);
        await fx.RelateAsync(foreign, await fx.RequestAsync(T0), T0.AddMinutes(30));

        Assert.Equal(1, (await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.IncidentToRequest)).Value);
        Assert.Equal(0, (await RunAsync(fx, fx.World.Desk.Id, TransferMetrics.IncidentToRequest)).Value);
    }

    [Fact]
    public async Task It_staff_never_see_assignment_rows_of_foreign_team_tickets()
    {
        var fx = await FixtureAsync();
        var foreign = await fx.IncidentAsync(T0, teamId: fx.World.FieldTeam.Id, requesterId: fx.World.Employee.Id);
        await fx.AssignAsync(foreign, fx.World.DeskTeam.Id, null, T0.AddMinutes(5));
        await fx.AssignAsync(foreign, fx.World.FieldTeam.Id, null, T0.AddMinutes(10)); // ends up in the other team
        var own = await fx.IncidentAsync(T0);
        await fx.AssignAsync(own, fx.World.FieldTeam.Id, null, T0.AddMinutes(5));
        await fx.AssignAsync(own, fx.World.DeskTeam.Id, null, T0.AddMinutes(10));

        Assert.Equal(4, (await RunAsync(fx, fx.World.Admin.Id, TransferMetrics.TransferCount)).Value);
        Assert.Equal(2, (await RunAsync(fx, fx.World.Desk.Id, TransferMetrics.TransferCount)).Value);
        Assert.Equal(1, (await RunAsync(fx, fx.World.Desk.Id, TransferMetrics.TransferRate)).Denominator);
    }
}
