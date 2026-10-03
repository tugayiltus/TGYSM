using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class RequestReportTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly DateTimeOffset Now = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly ReportFilter March10 = new() { From = new DateOnly(2026, 3, 10), To = new DateOnly(2026, 3, 10) };

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private static long Bucket(ReportFigure figure, string key) => Assert.Single(figure.Buckets!, b => b.Key == key).Count;

    private async Task<ReportFixture> FixtureAsync()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var fx = new ReportFixture(world);
        var h = TimeSpan.FromHours(1);
        var m = TimeSpan.FromMinutes(1);

        // R1: approved after 30 min, fulfilled after 3 h (2 h SLA time), met.
        var r1 = await fx.RequestAsync(T0, requiresApproval: true, t =>
        {
            t.ApprovalGranted(T0 + (30 * m));
            t.Complete("verildi", null, false, true, T0 + (3 * h));
        });
        await fx.ApprovalAsync(r1, T0, T0 + (30 * m));
        await fx.SlaAsync(r1, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, 3 * h, h, SlaResult.Met));
        // R2: no approval, fulfilled after 1 h, breached.
        var r2 = await fx.RequestAsync(T0, change: t => t.Complete("verildi", null, false, false, T0 + h));
        await fx.SlaAsync(r2, SlaMetric.Resolution, T0, ReportFixture.StopAfter(T0, h, TimeSpan.Zero, SlaResult.Breached));
        // R3: approval rejected after 2 h.
        var r3 = await fx.RequestAsync(T0, requiresApproval: true, t => t.ApprovalRejected("olmaz", T0 + (2 * h)));
        await fx.ApprovalAsync(r3, T0, T0 + (2 * h), approve: false);
        // R4: cancelled, with an approval still pending (never decided).
        var r4 = await fx.RequestAsync(T0, requiresApproval: true, t => t.Cancel("vazgecildi", T0 + h));
        await fx.ApprovalAsync(r4, T0, decided: null);
        // R5: approved a month later, outside the window.
        var r5 = await fx.RequestAsync(T0, requiresApproval: true);
        await fx.ApprovalAsync(r5, T0, T0.AddDays(30));
        // An incident is never part of the request report.
        await fx.IncidentAsync(T0);
        return fx;
    }

    [Fact]
    public async Task Volume_counts_requests_created_in_the_window()
    {
        var fx = await FixtureAsync();

        var result = await fx.RunAsync(ReportName.Requests, fx.World.Admin.Id, March10, Now);

        Assert.Equal(5, Figure(result, MetricKeys.Volume).Value);
    }

    [Fact]
    public async Task Fulfilment_time_averages_fulfilled_requests()
    {
        var fx = await FixtureAsync();

        var figure = Figure(await fx.RunAsync(ReportName.Requests, fx.World.Admin.Id, March10, Now), RequestMetrics.FulfilmentTime);

        Assert.Equal(2, figure.Denominator);
        Assert.Equal(2 * 3600, figure.Value); // (3 h + 1 h) / 2
        Assert.Equal(2 * 3600, Bucket(figure, MetricKeys.Calendar));
        Assert.Equal(1.5 * 3600, Bucket(figure, MetricKeys.Sla)); // (2 h + 1 h) / 2
    }

    [Fact]
    public async Task Approval_wait_counts_only_decided_approvals_in_the_window()
    {
        var fx = await FixtureAsync();

        var figure = Figure(await fx.RunAsync(ReportName.Requests, fx.World.Admin.Id, March10, Now), RequestMetrics.ApprovalWait);

        Assert.Equal(2, figure.Denominator); // R1 and R3: not the pending one, not the one decided in April
        Assert.Equal((1800 + 7200) / 2.0, figure.Value);
    }

    [Fact]
    public async Task Rejection_rate_divides_rejected_by_all_completed_requests()
    {
        var fx = await FixtureAsync();

        var figure = Figure(await fx.RunAsync(ReportName.Requests, fx.World.Admin.Id, March10, Now), RequestMetrics.RejectionRate);

        Assert.Equal(1, figure.Numerator);
        Assert.Equal(3, figure.Denominator); // R1, R2 fulfilled and R3 rejected; the plain cancellation is apart
        Assert.Equal(1 / 3.0, figure.Value);
    }

    [Fact]
    public async Task Compliance_and_excluded_outcomes_follow_the_same_rules_as_incidents()
    {
        var fx = await FixtureAsync();

        var result = await fx.RunAsync(ReportName.Requests, fx.World.Admin.Id, March10, Now);

        var compliance = Figure(result, MetricKeys.SlaCompliance);
        Assert.Equal(1, compliance.Numerator);
        Assert.Equal(2, compliance.Denominator);
        var excluded = Figure(result, MetricKeys.ExcludedOutcomes);
        Assert.Equal(1, Bucket(excluded, MetricKeys.Cancelled));
        Assert.Equal(1, Bucket(excluded, RequestMetrics.ApprovalRejected));
    }

    [Fact]
    public async Task Empty_window_gives_null_values_and_zero_denominators()
    {
        var fx = await FixtureAsync();
        var empty = new ReportFilter { From = new DateOnly(2025, 1, 1), To = new DateOnly(2025, 1, 31) };

        var result = await fx.RunAsync(ReportName.Requests, fx.World.Admin.Id, empty, Now);

        foreach (var key in new[] { RequestMetrics.RejectionRate, MetricKeys.SlaCompliance })
        {
            Assert.Null(Figure(result, key).Value);
            Assert.Equal(0, Figure(result, key).Denominator);
        }

        Assert.Null(Figure(result, RequestMetrics.ApprovalWait).Value);
        Assert.Null(Figure(result, RequestMetrics.FulfilmentTime).Value);
    }

    [Fact]
    public async Task Every_figure_counts_exactly_the_rows_it_exposes()
    {
        var fx = await FixtureAsync();

        await fx.AssertFiguresMatchRowsAsync(ReportName.Requests, March10, Now);
    }
}
