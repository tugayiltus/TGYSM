using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Notifications;
using Tgysm.Application.Sla;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Sla;

namespace Tgysm.Api.IntegrationTests.Sla;

/// <summary>
/// <see cref="MonitorSlaHandler"/> against real tickets (ADR-0014, ADR-0018, spec §5.4). A P3 incident starts on a
/// Monday 10:00: first response warns at 13:00 (75 % of 240 business minutes) and is due at 14:00; its resolution
/// target is days away, so these tests only ever cross the first-response thresholds unless they say otherwise.
/// </summary>
public sealed class SlaMonitorTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const int Batch = 50;
    private const int ParallelInstances = 20;
    private const int PastWarningMinutes = 181;
    private const int PastDueMinutes = 241;
    private const int ForgottenDays = 14;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    // See SlaTrackingTests: a FakeTimeProvider only moves forward, so each test starts on the next free Monday 10:00 Istanbul.
    private static readonly DateTimeOffset FirstStart = new(2026, 10, 5, 7, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OneWeek = TimeSpan.FromDays(7);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        var start = FirstStart;
        while (start <= factory.Clock.GetUtcNow())
        {
            start += OneWeek;
        }

        factory.Clock.SetUtcNow(start);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<(int Warned, int Breached)> MonitorAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MonitorSlaHandler>().HandleAsync(Batch, Ct);
    }

    private async Task<List<OutboxEvent>> OutboxAsync(string eventType)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.OutboxEvents.AsNoTracking().Where(e => e.EventType == eventType).ToListAsync(Ct);
    }

    private async Task<int> EventCountAsync(SlaEventKind kind)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.SlaEvents.CountAsync(e => e.Kind == kind, Ct);
    }

    private async Task<SlaInstance> InstanceAsync(JsonElement ticket, SlaMetric metric)
    {
        await using var db = factory.Database.CreateDbContext();
        var id = await db.Tickets.Where(t => t.Number == TicketWorld.NumberOf(ticket)).Select(t => t.Id).SingleAsync(Ct);
        return await db.SlaInstances.AsNoTracking().SingleAsync(i => i.TicketId == id && i.Metric == metric, Ct);
    }

    [Fact]
    public async Task Crossing_the_warning_threshold_emits_one_warning()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromMinutes(PastWarningMinutes));

        var first = await MonitorAsync();
        var second = await MonitorAsync();

        Assert.Equal((1, 0), first);
        Assert.Equal((0, 0), second);
        var warning = Assert.Single(await OutboxAsync(NotificationEventTypes.SlaWarning));
        Assert.Equal(ticket.GetProperty("id").GetGuid(), warning.TicketId);
        using var payload = JsonDocument.Parse(warning.Payload);
        Assert.Equal(SlaMetricLabels.FirstResponseKey, payload.RootElement.GetProperty(OutboxEvent.SlaMetricKey).GetString());
        var dueAt = (await InstanceAsync(ticket, SlaMetric.FirstResponse)).DueAt;
        Assert.Equal(dueAt, payload.RootElement.GetProperty(OutboxEvent.SlaDueAtKey).GetDateTimeOffset());
        Assert.Equal(1, await EventCountAsync(SlaEventKind.Warning));
        Assert.NotNull((await InstanceAsync(ticket, SlaMetric.FirstResponse)).WarnedAt);
        Assert.Empty(await OutboxAsync(NotificationEventTypes.SlaBreached));
    }

    [Fact]
    public async Task A_holiday_added_on_the_due_day_moves_due_forward_and_the_monitor_does_not_breach_at_the_old_due_time()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var oldDueAt = (await InstanceAsync(ticket, SlaMetric.FirstResponse)).DueAt!.Value;
        factory.Clock.Advance(TimeSpan.FromHours(1));

        // Monday 11:00 Istanbul: today becomes a holiday. 1 h already ran; the remaining 3 h start Tuesday 09:00.
        var istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(factory.Clock.GetUtcNow(), istanbul).DateTime);
        using var add = await world.Admin.PostAsync("/api/v1/admin/sla/holidays",
            new { date = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), name = "Olağanüstü tatil" });
        Assert.Equal(System.Net.HttpStatusCode.Created, add.StatusCode);

        var instance = await InstanceAsync(ticket, SlaMetric.FirstResponse);
        var tuesdayNoon = new DateTimeOffset(today.AddDays(1).ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(3));
        Assert.Equal(tuesdayNoon, instance.DueAt);
        // Both running timers of the ticket (first response and resolution) are re-planned.
        Assert.Equal(2, await EventCountAsync(SlaEventKind.CalendarChanged));

        factory.Clock.SetUtcNow(oldDueAt + TimeSpan.FromMinutes(1));
        Assert.Equal((0, 0), await MonitorAsync());
        Assert.Empty(await OutboxAsync(NotificationEventTypes.SlaBreached));
    }

    [Fact]
    public async Task Nothing_is_emitted_before_the_warning_threshold()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromMinutes(PastWarningMinutes - 5));

        Assert.Equal((0, 0), await MonitorAsync());
        Assert.Empty(await OutboxAsync(NotificationEventTypes.SlaWarning));
    }

    [Fact]
    public async Task Crossing_due_emits_one_breach_and_a_warning_when_none_was_sent()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromMinutes(PastDueMinutes));

        var first = await MonitorAsync();
        var second = await MonitorAsync();

        Assert.Equal((1, 1), first);
        Assert.Equal((0, 0), second);
        Assert.Single(await OutboxAsync(NotificationEventTypes.SlaWarning));
        Assert.Single(await OutboxAsync(NotificationEventTypes.SlaBreached));
        var instance = await InstanceAsync(ticket, SlaMetric.FirstResponse);
        Assert.NotNull(instance.WarnedAt);
        Assert.NotNull(instance.BreachedAt);
    }

    [Fact]
    public async Task Breach_after_an_earlier_warning_emits_only_the_breach()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromMinutes(PastWarningMinutes));
        Assert.Equal((1, 0), await MonitorAsync());
        factory.Clock.Advance(TimeSpan.FromMinutes(PastDueMinutes - PastWarningMinutes));

        Assert.Equal((0, 1), await MonitorAsync());

        Assert.Single(await OutboxAsync(NotificationEventTypes.SlaWarning));
        Assert.Single(await OutboxAsync(NotificationEventTypes.SlaBreached));
    }

    [Fact]
    public async Task Paused_instance_never_warns_or_breaches()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Employee, approval: true);
        factory.Clock.Advance(TimeSpan.FromDays(ForgottenDays));

        var result = await MonitorAsync();

        // Only the running first-response timer crosses its thresholds; the resolution timer is paused for the approval.
        Assert.Equal((1, 1), result);
        var resolution = await InstanceAsync(request, SlaMetric.Resolution);
        Assert.True(resolution.IsPaused);
        Assert.Null(resolution.WarnedAt);
        Assert.Null(resolution.BreachedAt);
    }

    [Fact]
    public async Task Stopped_instance_never_warns()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        using var cancel = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "cancel"), new { version = TicketWorld.VersionOf(ticket), reason = "Vazgeçildi" });
        Assert.True(cancel.IsSuccessStatusCode);
        factory.Clock.Advance(TimeSpan.FromDays(ForgottenDays));

        Assert.Equal((0, 0), await MonitorAsync());
    }

    [Fact]
    public async Task Two_parallel_runs_over_twenty_due_instances_warn_each_exactly_once()
    {
        var world = await TicketWorld.CreateAsync(factory);
        for (var i = 0; i < ParallelInstances; i++)
        {
            await TicketWorld.CreateIncidentAsync(world.Employee, subject: $"Yazıcı arızası {i}");
        }

        factory.Clock.Advance(TimeSpan.FromMinutes(PastWarningMinutes));

        var results = await Task.WhenAll(MonitorAsync(), MonitorAsync());

        Assert.Equal(ParallelInstances, results.Sum(r => r.Warned));
        Assert.Equal(0, results.Sum(r => r.Breached));
        Assert.Equal(ParallelInstances, (await OutboxAsync(NotificationEventTypes.SlaWarning)).Count);
        Assert.Equal(ParallelInstances, await EventCountAsync(SlaEventKind.Warning));
    }

    [Fact]
    public async Task Monitor_run_does_not_fail_a_ticket_action_using_an_instance_loaded_before_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        await using var db = factory.Database.CreateDbContext();
        var ticketId = await db.Tickets.Where(t => t.Number == TicketWorld.NumberOf(ticket)).Select(t => t.Id).SingleAsync(Ct);
        var stale = await db.SlaInstances.SingleAsync(i => i.TicketId == ticketId && i.Metric == SlaMetric.FirstResponse, Ct);
        factory.Clock.Advance(TimeSpan.FromMinutes(PastWarningMinutes));
        Assert.Equal((1, 0), await MonitorAsync());

        // What a ticket action does to its instance: change it and save, from the copy read before the monitor ran.
        stale.RecordBreach(factory.Clock.GetUtcNow());
        await db.SaveChangesAsync(Ct);

        var stored = await InstanceAsync(ticket, SlaMetric.FirstResponse);
        Assert.NotNull(stored.WarnedAt);
        Assert.NotNull(stored.BreachedAt);
    }

    [Fact]
    public async Task Warning_goes_to_the_assignee_and_an_unassigned_ticket_goes_to_the_team_members()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var unassigned = await TicketWorld.CreateIncidentAsync(world.Employee);
        var assigned = await TicketWorld.CreateIncidentAsync(world.Employee, subject: "Atanmış kayıt");
        using var claim = await world.Desk.PostAsync(TicketWorld.PathOf(assigned, "claim"), new { version = TicketWorld.VersionOf(assigned) });
        Assert.True(claim.IsSuccessStatusCode);
        factory.Clock.Advance(TimeSpan.FromMinutes(PastWarningMinutes));
        Assert.Equal((2, 0), await MonitorAsync());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(Batch * 2, Lease, Ct);
        }

        await using var db = factory.Database.CreateDbContext();
        var recipientsByTicket = (await db.EmailDeliveries.AsNoTracking()
                .Join(db.OutboxEvents, d => d.OutboxId, e => e.Id, (d, e) => new { d.RecipientUserId, d.TemplateType, e.TicketId })
                .Where(x => x.TemplateType == NotificationEventTypes.SlaWarning)
                .ToListAsync(Ct))
            .GroupBy(x => x.TicketId)
            .ToDictionary(g => g.Key!.Value, g => g.Select(x => x.RecipientUserId).ToList());
        var bodies = await db.EmailDeliveries.AsNoTracking().Where(d => d.TemplateType == NotificationEventTypes.SlaWarning).Select(d => d.Body).ToListAsync(Ct);
        Assert.All(bodies, body => Assert.Contains("ilk yanıt süresi dolmak üzere", body));
        Assert.Equal([world.DeskUser.Id], recipientsByTicket[assigned.GetProperty("id").GetGuid()]);
        Assert.Contains(world.DeskUser.Id, recipientsByTicket[unassigned.GetProperty("id").GetGuid()]);
        Assert.DoesNotContain(world.EmployeeUser.Id, recipientsByTicket[unassigned.GetProperty("id").GetGuid()]);
    }
}
