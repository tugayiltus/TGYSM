using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using Tgysm.Application.Reports;
using Tgysm.Application.Reports.Metrics;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>Fixed-time fixture helpers shared by the incident and request report tests.</summary>
internal sealed class ReportFixture(ReportWorld world)
{
    public const int TargetMinutes = 60;
    public const int WarningPercent = 75;
    public static readonly DateTimeOffset T0 = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly BusinessCalendarRules Rules = new(
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"),
        Enum.GetValues<DayOfWeek>().ToDictionary(d => d, _ => new WorkingRange(new TimeOnly(0, 0), new TimeOnly(23, 59))),
        new HashSet<DateOnly>());

    private int _counter;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReportWorld World { get; } = world;

    public async Task<Ticket> IncidentAsync(DateTimeOffset created, Action<Ticket>? change = null)
    {
        var ticket = Ticket.CreateIncident($"INC-2026-{++_counter:D6}", "Konu", "Aciklama", null, null, World.Employee.Id, World.DeskTeam.Id, created);
        return await SaveAsync(ticket, change);
    }

    public async Task<Ticket> RequestAsync(DateTimeOffset created, bool requiresApproval = false, Action<Ticket>? change = null)
    {
        var type = await TestDataRequestTypeAsync(requiresApproval);
        var ticket = Ticket.CreateRequest(
            $"REQ-2026-{++_counter:D6}", "Konu", "Aciklama", null, null, World.Employee.Id, World.DeskTeam.Id, type, requiresApproval, created);
        return await SaveAsync(ticket, change);
    }

    /// <summary>A resolution (or first response) timer started at <paramref name="start"/>; <paramref name="change"/> pauses and stops it.</summary>
    public async Task SlaAsync(Ticket ticket, SlaMetric metric, DateTimeOffset start, Action<SlaInstance> change)
    {
        await using var db = World.Db();
        var version = await db.SlaPolicyVersions.Select(v => v.Id).FirstAsync(Ct);
        var target = SlaTarget.Create(version, ticket.Kind, Priority.P3, null, metric, TargetMinutes, WarningPercent);
        var instance = SlaInstance.Start(ticket.Id, metric, target, version, start, Rules, false, null);
        change(instance);
        db.SlaInstances.Add(instance);
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>Runs for [start, start+held] paused, so the SLA time is shorter than the calendar time by <paramref name="held"/>.</summary>
    public static Action<SlaInstance> StopAfter(DateTimeOffset start, TimeSpan total, TimeSpan held, SlaResult? result = null) =>
        instance =>
        {
            if (held > TimeSpan.Zero)
            {
                instance.Pause(HoldReason.User, start.AddMinutes(1), Rules);
                instance.Resume(start.AddMinutes(1) + held, Rules);
            }

            instance.Stop(result, start + total, Rules);
        };

    public async Task ApprovalAsync(Ticket ticket, DateTimeOffset created, DateTimeOffset? decided, bool approve = true)
    {
        var approval = Approval.Request(ticket.Id, World.Admin.Id, created);
        if (decided is { } at && approve)
        {
            approval.Approve(World.Admin.Id, at);
        }
        else if (decided is { } rejectedAt)
        {
            approval.Reject(World.Admin.Id, "olmaz", rejectedAt);
        }

        await using var db = World.Db();
        db.Approvals.Add(approval);
        await db.SaveChangesAsync(Ct);
    }

    public async Task<ReportResult> RunAsync(ReportName report, Guid userId, ReportFilter filter, DateTimeOffset now)
    {
        await using var db = World.Db();
        return await HandlerFor(db, userId, now).HandleAsync(new GetReportQuery(report, filter), Ct);
    }

    public GetReportHandler HandlerFor(Tgysm.Infrastructure.Persistence.AppDbContext db, Guid userId, DateTimeOffset now)
    {
        var clock = new FakeTimeProvider(now);
        return new GetReportHandler(new ReportScope(db, new FixedActor(userId)), new TicketReportSource(db, new TestClock()), db, clock);
    }

    /// <summary>Checks every metric of <paramref name="report"/>: its figure counts exactly the rows (and denominator rows) it exposes.</summary>
    public async Task AssertFiguresMatchRowsAsync(ReportName report, ReportFilter filter, DateTimeOffset now)
    {
        await using var db = World.Db();
        var scope = new ReportScope(db, new FixedActor(World.Admin.Id));
        var source = new TicketReportSource(db, new TestClock());
        var context = new ReportContext(db, now);
        var effective = ReportCatalog.EffectiveFilter(report, filter);
        foreach (var metric in ReportCatalog.Get(report).Cast<ReportMetric>())
        {
            var filtered = source.Apply(await scope.TicketsAsync(Ct), effective, metric.DateBasis);
            var figure = await metric.ComputeAsync(filtered, context, Ct);
            var rows = await metric.Rows(filtered, context).CountAsync(Ct);
            var denominator = metric.DenominatorRows(filtered, context);
            switch (metric.Shape)
            {
                case MetricShape.Count:
                    Assert.Equal(rows, (long)figure.Value!.Value);
                    break;
                case MetricShape.Distribution:
                    Assert.Equal(rows, (long)figure.Value!.Value);
                    Assert.Equal(rows, figure.Buckets!.Sum(b => b.Count));
                    break;
                case MetricShape.Duration:
                    Assert.Equal(rows, figure.Denominator);
                    break;
                default:
                    Assert.NotNull(denominator);
                    Assert.Equal(await denominator.CountAsync(Ct), figure.Denominator);
                    if (metric.Key != IncidentMetrics.Mttr)
                    {
                        Assert.Equal(rows, figure.Numerator);
                    }

                    break;
            }
        }
    }

    private async Task<Guid> TestDataRequestTypeAsync(bool requiresApproval) =>
        (await TestData.RequestTypeAsync(World.Factory, $"Tur {++_counter}", requiresApproval)).Id;

    private async Task<Ticket> SaveAsync(Ticket ticket, Action<Ticket>? change)
    {
        change?.Invoke(ticket);
        await using var db = World.Db();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(Ct);
        return ticket;
    }
}
