using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class IncidentReportTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly DateTimeOffset Now = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly ReportFilter March10 = new() { From = new DateOnly(2026, 3, 10), To = new DateOnly(2026, 3, 10) };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private static long Bucket(ReportFigure figure, string key) => Assert.Single(figure.Buckets!, b => b.Key == key).Count;

    private async Task<(ReportFixture Fixture, ReportWorld World)> ResolvedFixtureAsync()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var fx = new ReportFixture(world);
        var h = TimeSpan.FromHours(1);

        // A: resolved after 4 h, 3 h of SLA time (1 h on hold), met.
        var a = await fx.IncidentAsync(T0, t => t.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0 + (4 * h)));
        await fx.SlaAsync(a, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, 4 * h, h, SlaResult.Met));
        // B: resolved after 2 h, 2 h of SLA time, breached.
        var b = await fx.IncidentAsync(T0, t => t.Complete("cozuldu", TicketOutcome.Resolved, false, false, T0 + (2 * h)));
        await fx.SlaAsync(b, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, 2 * h, TimeSpan.Zero, SlaResult.Breached));
        // C: needs a service request: excluded from MTTR and compliance.
        var c = await fx.IncidentAsync(T0, t => t.Complete("talep", TicketOutcome.ServiceRequestRequired, true, false, T0 + h));
        await fx.SlaAsync(c, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, h, TimeSpan.Zero, SlaResult.Met));
        // D: cancelled.
        var d = await fx.IncidentAsync(T0, t => t.Cancel("vazgecildi", T0 + h));
        await fx.SlaAsync(d, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, h, TimeSpan.Zero, SlaResult.Cancelled));
        // E: resolved a month later, outside the window.
        await fx.IncidentAsync(T0, t => t.Complete("gec", TicketOutcome.Resolved, false, false, T0.AddDays(30)));
        // A request created in the window is never an incident.
        await fx.RequestAsync(T0);
        return (fx, world);
    }

    [Fact]
    public async Task Volume_counts_incidents_created_in_the_window_only()
    {
        var (fx, world) = await ResolvedFixtureAsync();
        await fx.IncidentAsync(T0.AddDays(10));

        var result = await fx.RunAsync(ReportName.Incidents, world.Admin.Id, March10, Now);

        var volume = Figure(result, MetricKeys.Volume);
        Assert.Equal(MetricShape.Count, volume.Shape);
        Assert.Equal(5, volume.Value);
        Assert.Equal("all", result.ScopeLabel);
    }

    [Fact]
    public async Task Mttr_uses_only_resolved_incidents_and_sums_calendar_and_sla_seconds()
    {
        var (fx, world) = await ResolvedFixtureAsync();

        var mttr = Figure(await fx.RunAsync(ReportName.Incidents, world.Admin.Id, March10, Now), IncidentMetrics.Mttr);

        Assert.Equal(6 * 3600, mttr.Numerator); // 4 h + 2 h calendar
        Assert.Equal(2, mttr.Denominator); // A and B only: not the service-request-required or cancelled ones
        Assert.Equal(3 * 3600, mttr.Value);
        Assert.Equal(6 * 3600, Bucket(mttr, IncidentMetrics.MttrCalendar));
        Assert.Equal(5 * 3600, Bucket(mttr, IncidentMetrics.MttrSla)); // 3 h + 2 h ElapsedSeconds
    }

    [Fact]
    public async Task Resolution_time_averages_calendar_and_sla_seconds_of_resolved_incidents()
    {
        var (fx, world) = await ResolvedFixtureAsync();

        var figure = Figure(await fx.RunAsync(ReportName.Incidents, world.Admin.Id, March10, Now), IncidentMetrics.ResolutionTime);

        Assert.Equal(MetricShape.Duration, figure.Shape);
        Assert.Equal(3 * 3600, figure.Value);
        Assert.Equal(2, figure.Denominator);
        Assert.Equal(3 * 3600, Bucket(figure, MetricKeys.Calendar));
        Assert.Equal(2.5 * 3600, Bucket(figure, MetricKeys.Sla));
    }

    [Fact]
    public async Task Compliance_counts_met_over_eligible_completed_incidents()
    {
        var (fx, world) = await ResolvedFixtureAsync();

        var figure = Figure(await fx.RunAsync(ReportName.Incidents, world.Admin.Id, March10, Now), MetricKeys.SlaCompliance);

        Assert.Equal(1, figure.Numerator);
        Assert.Equal(2, figure.Denominator);
        Assert.Equal(0.5, figure.Value);
    }

    [Fact]
    public async Task Excluded_outcomes_count_cancelled_and_service_request_required_apart()
    {
        var (fx, world) = await ResolvedFixtureAsync();

        var figure = Figure(await fx.RunAsync(ReportName.Incidents, world.Admin.Id, March10, Now), MetricKeys.ExcludedOutcomes);

        Assert.Equal(2, figure.Value);
        Assert.Equal(1, Bucket(figure, MetricKeys.Cancelled));
        Assert.Equal(1, Bucket(figure, IncidentMetrics.ServiceRequestRequired));
    }

    [Fact]
    public async Task Empty_window_gives_null_values_and_zero_denominators()
    {
        var (fx, world) = await ResolvedFixtureAsync();
        var empty = new ReportFilter { From = new DateOnly(2025, 1, 1), To = new DateOnly(2025, 1, 31) };

        var result = await fx.RunAsync(ReportName.Incidents, world.Admin.Id, empty, Now);

        foreach (var key in new[] { IncidentMetrics.Mttr, MetricKeys.SlaCompliance })
        {
            var figure = Figure(result, key);
            Assert.Null(figure.Value);
            Assert.Equal(0, figure.Denominator);
        }

        Assert.Null(Figure(result, IncidentMetrics.ResolutionTime).Value);
        Assert.Null(Figure(result, IncidentMetrics.FirstResponseTime).Value);
    }

    [Fact]
    public async Task First_response_time_averages_stopped_timers_in_the_window_and_skips_cancelled_ones()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var fx = new ReportFixture(world);
        var m = TimeSpan.FromMinutes(1);
        var fast = await fx.IncidentAsync(T0);
        await fx.SlaAsync(fast, SlaMetric.FirstResponse, T0, ReportFixture.StopAfter(T0, 30 * m, 10 * m)); // 30 min, 20 min SLA
        var slow = await fx.IncidentAsync(T0);
        await fx.SlaAsync(slow, SlaMetric.FirstResponse, T0, ReportFixture.StopAfter(T0, 90 * m, TimeSpan.Zero)); // 90 min
        var cancelled = await fx.IncidentAsync(T0);
        await fx.SlaAsync(cancelled, SlaMetric.FirstResponse, T0, ReportFixture.StopAfter(T0, 5 * m, TimeSpan.Zero, SlaResult.Cancelled));
        var late = await fx.IncidentAsync(T0);
        await fx.SlaAsync(late, SlaMetric.FirstResponse, T0.AddDays(30), ReportFixture.StopAfter(T0.AddDays(30), 5 * m, TimeSpan.Zero));
        var waiting = await fx.IncidentAsync(T0);
        await fx.SlaAsync(waiting, SlaMetric.FirstResponse, T0, _ => { });

        var figure = Figure(await fx.RunAsync(ReportName.Incidents, world.Admin.Id, March10, Now), IncidentMetrics.FirstResponseTime);

        Assert.Equal(2, figure.Denominator);
        Assert.Equal(60 * 60, figure.Value);
        Assert.Equal(60 * 60, Bucket(figure, MetricKeys.Calendar));
        Assert.Equal(55 * 60, Bucket(figure, MetricKeys.Sla)); // (20 + 90) / 2 min
    }

    [Fact]
    public async Task Open_backlog_buckets_split_at_exact_day_edges_and_ignore_the_window()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var fx = new ReportFixture(world);
        foreach (var days in new[] { 1, 2, 3, 4, 7, 8 })
        {
            await fx.IncidentAsync(Now.AddDays(-days));
        }

        await fx.IncidentAsync(Now.AddDays(-5), t => t.Complete("bitti", TicketOutcome.Resolved, false, false, Now.AddDays(-4)));
        await fx.IncidentAsync(Now.AddDays(-5), t => t.Cancel("iptal", Now.AddDays(-4)));
        await fx.IncidentAsync(Now.AddDays(-5), t => t.Hold(HoldReason.User, null, Now.AddDays(-4))); // on hold is still open

        var result = await fx.RunAsync(ReportName.Incidents, world.Admin.Id, March10, Now);

        var backlog = Figure(result, IncidentMetrics.OpenBacklog);
        Assert.Equal(MetricShape.Distribution, backlog.Shape);
        Assert.Equal(7, backlog.Value);
        Assert.Equal(1, Bucket(backlog, "0-1"));
        Assert.Equal(2, Bucket(backlog, "2-3"));
        Assert.Equal(3, Bucket(backlog, "4-7")); // 4, 7 and the held ticket (5)
        Assert.Equal(1, Bucket(backlog, "8+"));
    }

    [Fact]
    public async Task Kind_filter_is_ignored_because_the_report_fixes_the_kind()
    {
        var (fx, world) = await ResolvedFixtureAsync();

        var result = await fx.RunAsync(ReportName.Incidents, world.Admin.Id, March10 with { Kind = TicketKind.Request }, Now);

        Assert.Equal(5, Figure(result, MetricKeys.Volume).Value); // still the 5 incidents, never the request
    }

    [Fact]
    public async Task Staff_sees_only_own_teams_and_employee_is_forbidden()
    {
        var (fx, world) = await ResolvedFixtureAsync();

        var desk = await fx.RunAsync(ReportName.Incidents, world.Desk.Id, March10, Now);
        var field = await fx.RunAsync(ReportName.Incidents, world.Field.Id, March10, Now);

        Assert.Equal("teams", desk.ScopeLabel);
        Assert.Equal(5, Figure(desk, MetricKeys.Volume).Value);
        Assert.Equal(0, Figure(field, MetricKeys.Volume).Value);
        await Assert.ThrowsAsync<ForbiddenException>(() => fx.RunAsync(ReportName.Incidents, world.Employee.Id, March10, Now));
    }

    [Fact]
    public async Task Every_figure_counts_exactly_the_rows_it_exposes()
    {
        var (fx, _) = await ResolvedFixtureAsync();

        await fx.AssertFiguresMatchRowsAsync(ReportName.Incidents, March10, Now);
    }
}
