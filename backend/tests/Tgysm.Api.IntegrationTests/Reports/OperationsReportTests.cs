using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class OperationsReportTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly DateTimeOffset Now = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly ReportFilter March10 = new() { From = new DateOnly(2026, 3, 10), To = new DateOnly(2026, 3, 10) };

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private static ReportBucket Bucket(ReportFigure figure, string key) => Assert.Single(figure.Buckets!, b => b.Key == key);

    private async Task<ReportFixture> FixtureAsync() => new(await ReportWorld.CreateAsync(factory));

    private static async Task<ReportFigure> RunAsync(ReportFixture fx, Guid userId, ReportFilter filter, string metric) =>
        Figure(await fx.RunAsync(ReportName.Operations, userId, filter, Now), metric);

    [Fact]
    public async Task Team_load_counts_open_tickets_now_and_ignores_the_date_window()
    {
        var fx = await FixtureAsync();
        await fx.IncidentAsync(T0.AddDays(-60)); // created long before the window, still open
        await fx.IncidentAsync(T0, t => t.Start(T0));
        await fx.IncidentAsync(T0, t => t.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0.AddHours(1)));
        await fx.IncidentAsync(T0, t => t.Cancel("vazgecildi", T0.AddHours(1)));
        await fx.IncidentAsync(T0, teamId: fx.World.FieldTeam.Id, requesterId: fx.World.Employee.Id);

        var figure = await RunAsync(fx, fx.World.Admin.Id, March10, OperationsMetrics.TeamLoad);

        Assert.Equal(3, figure.Value);
        Assert.Equal((2L, "Masa"), (Bucket(figure, fx.World.DeskTeam.Id.ToString()).Count, Bucket(figure, fx.World.DeskTeam.Id.ToString()).Label));
        Assert.Equal((1L, "Saha"), (Bucket(figure, fx.World.FieldTeam.Id.ToString()).Count, Bucket(figure, fx.World.FieldTeam.Id.ToString()).Label));
        await fx.AssertFiguresMatchRowsAsync(ReportName.Operations, March10, Now);
    }

    [Fact]
    public async Task Team_load_of_it_staff_covers_only_their_teams()
    {
        var fx = await FixtureAsync();
        await fx.IncidentAsync(T0);
        await fx.IncidentAsync(T0, teamId: fx.World.FieldTeam.Id, requesterId: fx.World.Employee.Id);

        var figure = await RunAsync(fx, fx.World.Desk.Id, March10, OperationsMetrics.TeamLoad);

        Assert.Equal(1, figure.Value);
        Assert.Equal(fx.World.DeskTeam.Id.ToString(), Assert.Single(figure.Buckets!).Key);
    }

    [Fact]
    public async Task Assignee_load_groups_open_tickets_and_names_the_unassigned()
    {
        var fx = await FixtureAsync();
        var assigned = await fx.IncidentAsync(T0);
        await fx.AssignAsync(assigned, fx.World.DeskTeam.Id, fx.World.Desk.Id, T0.AddMinutes(5));
        await fx.IncidentAsync(T0);
        await fx.IncidentAsync(T0);

        var figure = await RunAsync(fx, fx.World.Admin.Id, March10, OperationsMetrics.AssigneeLoad);

        Assert.Equal(3, figure.Value);
        Assert.Equal(1, Bucket(figure, fx.World.Desk.Id.ToString()).Count);
        Assert.Equal("Test User", Bucket(figure, fx.World.Desk.Id.ToString()).Label);
        Assert.Equal(2, Bucket(figure, OperationsMetrics.Unassigned).Count);
        await fx.AssertFiguresMatchRowsAsync(ReportName.Operations, March10, Now);
    }

    [Fact]
    public async Task Category_and_location_group_tickets_created_in_the_window()
    {
        var fx = await FixtureAsync();
        var network = await TestData.CategoryAsync(factory, "Ag");
        var printer = await TestData.CategoryAsync(factory, "Yazici");
        var hq = await TestData.LocationAsync(factory, "Merkez");
        await Save(fx, Ticket.CreateIncident("INC-2026-900001", "K", "A", network.Id, hq.Id, fx.World.Employee.Id, fx.World.DeskTeam.Id, T0));
        await Save(fx, Ticket.CreateIncident("INC-2026-900002", "K", "A", network.Id, null, fx.World.Employee.Id, fx.World.DeskTeam.Id, T0));
        await Save(fx, Ticket.CreateIncident("INC-2026-900003", "K", "A", printer.Id, hq.Id, fx.World.Employee.Id, fx.World.DeskTeam.Id, T0));
        await Save(fx, Ticket.CreateIncident("INC-2026-900004", "K", "A", null, null, fx.World.Employee.Id, fx.World.DeskTeam.Id, T0));
        await Save(fx, Ticket.CreateIncident("INC-2026-900005", "K", "A", network.Id, hq.Id, fx.World.Employee.Id, fx.World.DeskTeam.Id, T0.AddDays(-30))); // outside

        var categories = await RunAsync(fx, fx.World.Admin.Id, March10, OperationsMetrics.ByCategory);
        var locations = await RunAsync(fx, fx.World.Admin.Id, March10, OperationsMetrics.ByLocation);

        Assert.Equal(4, categories.Value);
        Assert.Equal((2L, "Ag"), (Bucket(categories, network.Id.ToString()).Count, Bucket(categories, network.Id.ToString()).Label));
        Assert.Equal(1, Bucket(categories, printer.Id.ToString()).Count);
        Assert.Equal(1, Bucket(categories, OperationsMetrics.None).Count);
        Assert.Equal((2L, "Merkez"), (Bucket(locations, hq.Id.ToString()).Count, Bucket(locations, hq.Id.ToString()).Label));
        Assert.Equal(2, Bucket(locations, OperationsMetrics.None).Count);
        await fx.AssertFiguresMatchRowsAsync(ReportName.Operations, March10, Now);
    }

    [Fact]
    public async Task Department_follows_the_requester()
    {
        var fx = await FixtureAsync();
        var finance = await TestData.DepartmentAsync(factory, "Muhasebe");
        var hr = await TestData.DepartmentAsync(factory, "Insan Kaynaklari");
        var a = await TestUsers.CreateAsync(factory, UserRole.Employee, "a@example.com", departmentId: finance.Id);
        var b = await TestUsers.CreateAsync(factory, UserRole.Employee, "b@example.com", departmentId: hr.Id);
        await fx.IncidentAsync(T0, teamId: fx.World.DeskTeam.Id, requesterId: a.Id);
        await fx.IncidentAsync(T0, teamId: fx.World.DeskTeam.Id, requesterId: a.Id);
        await fx.IncidentAsync(T0, teamId: fx.World.DeskTeam.Id, requesterId: b.Id);
        await fx.IncidentAsync(T0, teamId: fx.World.DeskTeam.Id, requesterId: fx.World.Employee.Id); // requester without department

        var figure = await RunAsync(fx, fx.World.Admin.Id, March10, OperationsMetrics.ByDepartment);

        Assert.Equal(2, Bucket(figure, finance.Id.ToString()).Count);
        Assert.Equal("Muhasebe", Bucket(figure, finance.Id.ToString()).Label);
        Assert.Equal(1, Bucket(figure, hr.Id.ToString()).Count);
        Assert.Equal(1, Bucket(figure, OperationsMetrics.None).Count);
        await fx.AssertFiguresMatchRowsAsync(ReportName.Operations, March10, Now);
    }

    [Fact]
    public async Task Hold_reasons_count_only_tickets_on_hold_now()
    {
        var fx = await FixtureAsync();
        await fx.IncidentAsync(T0, t => t.Hold(HoldReason.Vendor, null, T0.AddMinutes(1)));
        await fx.IncidentAsync(T0, t => t.Hold(HoldReason.Vendor, null, T0.AddMinutes(1)));
        await fx.IncidentAsync(T0, t => t.Hold(HoldReason.ScheduledDate, null, T0.AddMinutes(1)));
        await fx.IncidentAsync(T0, t =>
        {
            t.Hold(HoldReason.User, null, T0.AddMinutes(1));
            t.Resume(T0.AddMinutes(2)); // no longer on hold
        });
        await fx.IncidentAsync(T0.AddDays(-40), t => t.Hold(HoldReason.ThirdParty, null, T0.AddDays(-39))); // older than the window, still on hold

        var figure = await RunAsync(fx, fx.World.Admin.Id, March10, OperationsMetrics.HoldReasons);

        Assert.Equal(4, figure.Value);
        Assert.Equal(2, Bucket(figure, "vendor").Count);
        Assert.Equal(1, Bucket(figure, "scheduled_date").Count);
        Assert.Equal(1, Bucket(figure, "third_party").Count);
        Assert.DoesNotContain(figure.Buckets!, b => b.Key == "user");
        await fx.AssertFiguresMatchRowsAsync(ReportName.Operations, March10, Now);
    }

    [Fact]
    public async Task Team_completed_uses_the_team_at_completion_not_the_current_one()
    {
        var fx = await FixtureAsync();
        var h = TimeSpan.FromHours(1);
        // Moved to Saha at T0+1h, completed at T0+2h, then moved back to Masa at T0+5h: Saha completed it.
        var moved = Ticket.CreateIncident("INC-2026-900010", "K", "A", null, null, fx.World.Employee.Id, fx.World.DeskTeam.Id, T0);
        var first = moved.Assign(fx.World.FieldTeam.Id, null, T0 + h, fx.World.Admin.Id);
        // The row at T0+5h is after completion and must not count (written before Complete: the domain refuses moves on closed tickets).
        var afterRow = moved.Assign(fx.World.DeskTeam.Id, null, T0 + (5 * h), fx.World.Admin.Id);
        moved.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0 + (2 * h));
        var later = Ticket.CreateIncident("INC-2026-900011", "K", "A", null, null, fx.World.Employee.Id, fx.World.DeskTeam.Id, T0);
        var laterRow = later.Assign(fx.World.FieldTeam.Id, null, T0 + h, fx.World.Admin.Id);
        later.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0 + (2 * h));
        await using (var db = fx.World.Db())
        {
            db.Tickets.AddRange(moved, later);
            db.TicketAssignments.AddRange(first, afterRow, laterRow);
            await db.SaveChangesAsync(Ct);
        }

        // Never transferred: the current team completed it.
        await fx.IncidentAsync(T0, t => t.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0 + h));
        await fx.IncidentAsync(T0, t => t.Cancel("vazgecildi", T0 + h)); // cancelled is not completed

        var figure = await RunAsync(fx, fx.World.Admin.Id, March10, OperationsMetrics.TeamCompleted);

        Assert.Equal(3, figure.Value);
        Assert.Equal((1L, "Masa"), (Bucket(figure, fx.World.DeskTeam.Id.ToString()).Count, Bucket(figure, fx.World.DeskTeam.Id.ToString()).Label));
        Assert.Equal((2L, "Saha"), (Bucket(figure, fx.World.FieldTeam.Id.ToString()).Count, Bucket(figure, fx.World.FieldTeam.Id.ToString()).Label));
        await fx.AssertFiguresMatchRowsAsync(ReportName.Operations, March10, Now);
    }

    [Fact]
    public async Task The_result_carries_the_filter_the_report_really_ran_with()
    {
        var fx = await FixtureAsync();

        var result = await fx.RunAsync(ReportName.Incidents, fx.World.Admin.Id, new ReportFilter { Kind = TicketKind.Request }, Now);

        Assert.Equal(TicketKind.Incident, result.Filter.Kind);
    }

    private static async Task Save(ReportFixture fx, Ticket ticket)
    {
        await using var db = fx.World.Db();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(Ct);
    }
}
