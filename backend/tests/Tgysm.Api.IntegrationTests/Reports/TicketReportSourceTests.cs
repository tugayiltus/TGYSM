using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Reports;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class TicketReportSourceTests(ApiFactory factory) : IAsyncLifetime
{
    private const int TargetMinutes = 60;
    private const int WarningPercent = 75;
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly BusinessCalendarRules Rules = new(
        Istanbul,
        Enum.GetValues<DayOfWeek>().ToDictionary(d => d, _ => new WorkingRange(new TimeOnly(0, 0), new TimeOnly(23, 59))),
        new HashSet<DateOnly>());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static SlaInstance Instance(Guid ticketId, Guid version, Action<SlaInstance> change)
    {
        var target = SlaTarget.Create(version, TicketKind.Incident, Priority.P3, null, SlaMetric.Resolution, TargetMinutes, WarningPercent);
        var instance = SlaInstance.Start(ticketId, SlaMetric.Resolution, target, version, Now, Rules, false, null);
        change(instance);
        return instance;
    }

    private async Task<Guid> WithSlaAsync(ReportWorld world, Action<SlaInstance> change)
    {
        var ticket = await world.TicketAsync(world.Employee.Id, world.DeskTeam.Id);
        await using var db = world.Db();
        var version = await db.SlaPolicyVersions.Select(v => v.Id).FirstAsync(Ct);
        db.SlaInstances.Add(Instance(ticket.Id, version, change));
        await db.SaveChangesAsync(Ct);
        return ticket.Id;
    }

    private static async Task<List<Guid>> IdsAsync(ReportWorld world, SlaStateFilter state)
    {
        await using var db = world.Db();
        var source = new TicketReportSource(db, new TestClock());
        return await source.Apply(db.Tickets, new ReportFilter { SlaState = state }, ReportDateBasis.Created).Select(t => t.Id).ToListAsync(Ct);
    }

    [Fact]
    public async Task Each_sla_state_selects_its_fixture_ticket()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var met = await WithSlaAsync(world, i => i.Stop(SlaResult.Met, Now.AddMinutes(10), Rules));
        var breached = await WithSlaAsync(world, i => i.Stop(SlaResult.Breached, Now.AddMinutes(90), Rules));
        var openBreached = await WithSlaAsync(world, i => i.RecordBreach(Now.AddMinutes(61)));
        var atRisk = await WithSlaAsync(world, i => i.RecordWarning(Now.AddMinutes(46)));
        await WithSlaAsync(world, _ => { });

        Assert.Equal([met], await IdsAsync(world, SlaStateFilter.Met));
        Assert.Equal([breached], await IdsAsync(world, SlaStateFilter.Breached));
        Assert.Equal([openBreached], await IdsAsync(world, SlaStateFilter.OpenBreached));
        Assert.Equal([atRisk], await IdsAsync(world, SlaStateFilter.AtRisk));
    }

    [Fact]
    public async Task Open_breached_ticket_that_also_warned_is_not_at_risk()
    {
        var world = await ReportWorld.CreateAsync(factory);
        await WithSlaAsync(world, i =>
        {
            i.RecordWarning(Now.AddMinutes(46));
            i.RecordBreach(Now.AddMinutes(61));
        });

        Assert.Empty(await IdsAsync(world, SlaStateFilter.AtRisk));
        Assert.Single(await IdsAsync(world, SlaStateFilter.OpenBreached));
    }

    [Fact]
    public async Task Non_date_filters_and_date_basis_narrow_rows()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var march2 = new DateTimeOffset(2026, 3, 1, 21, 30, 0, TimeSpan.Zero);
        var inWindow = await world.TicketAsync(world.Employee.Id, world.DeskTeam.Id, march2, t => t.Complete("ok", TicketOutcome.Resolved, false, false, march2.AddDays(5)));
        await world.TicketAsync(world.Employee.Id, world.FieldTeam.Id, march2);
        await world.TicketAsync(world.Employee.Id, world.DeskTeam.Id, march2.AddDays(-1));

        await using var db = world.Db();
        var source = new TicketReportSource(db, new TestClock());
        var day = new DateOnly(2026, 3, 2);
        var created = await source.Apply(db.Tickets, new ReportFilter { From = day, To = day, TeamId = world.DeskTeam.Id }, ReportDateBasis.Created).Select(t => t.Id).ToListAsync(Ct);
        var completed = await source.Apply(db.Tickets, new ReportFilter { From = new DateOnly(2026, 3, 7), To = new DateOnly(2026, 3, 7) }, ReportDateBasis.Completed).Select(t => t.Id).ToListAsync(Ct);
        var byKind = await source.Apply(db.Tickets, new ReportFilter { Kind = TicketKind.Request }, ReportDateBasis.Created).CountAsync(Ct);

        Assert.Equal([inWindow.Id], created);
        Assert.Equal([inWindow.Id], completed);
        Assert.Equal(0, byKind);
    }
}
