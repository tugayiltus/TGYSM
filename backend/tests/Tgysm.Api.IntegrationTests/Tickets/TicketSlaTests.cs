using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Sla;

namespace Tgysm.Api.IntegrationTests.Tickets;

/// <summary>The <c>sla</c> block of ticket DTOs and the <c>sla_risk</c> queue (spec §5, §8). A P3 incident starts on a Monday 10:00 Istanbul.</summary>
public sealed class TicketSlaTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const int Batch = 50;
    private const int PastWarningMinutes = 181;
    private const int OneHourMinutes = 60;
    private const int FirstResponseTargetMinutes = 240;
    private const int SecondsPerMinute = 60;
    private const string FirstResponse = "first_response";
    private const string Resolution = "resolution";
    private const string SlaRiskQuery = "?queue=sla_risk";

    // A FakeTimeProvider only moves forward, so each test starts on the next free Monday 10:00 Istanbul.
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

    private async Task MonitorAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MonitorSlaHandler>().HandleAsync(Batch, Ct);
    }

    private static JsonElement Metric(JsonElement ticket, string metric) =>
        ticket.GetProperty("sla").EnumerateArray().Single(s => s.GetProperty("metric").GetString() == metric);

    private static async Task<List<string>> RiskAsync(ApiClient client)
    {
        var page = await TicketWorld.ReadAsync(client, $"{TicketWorld.TicketsPath}{SlaRiskQuery}");
        return [.. page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("number").GetString()!)];
    }

    [Fact]
    public async Task Detail_carries_both_timers_for_every_role_in_scope()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromMinutes(OneHourMinutes));

        foreach (var client in new[] { world.Employee, world.Desk, world.Admin })
        {
            var detail = await TicketWorld.ReadAsync(client, TicketWorld.PathOf(ticket));
            Assert.Equal(2, detail.GetProperty("sla").GetArrayLength());
            var first = Metric(detail, FirstResponse);
            Assert.Equal((FirstResponseTargetMinutes - OneHourMinutes) * SecondsPerMinute, first.GetProperty("remainingSeconds").GetInt64());
            Assert.NotEqual(JsonValueKind.Null, first.GetProperty("dueAt").ValueKind);
            Assert.False(first.GetProperty("isPaused").GetBoolean());
            Assert.False(first.GetProperty("isWarned").GetBoolean());
            Assert.False(first.GetProperty("isBreached").GetBoolean());
            Assert.Equal(JsonValueKind.Null, first.GetProperty("result").ValueKind);
            Assert.True(Metric(detail, Resolution).GetProperty("remainingSeconds").GetInt64() > first.GetProperty("remainingSeconds").GetInt64());
        }
    }

    [Fact]
    public async Task List_items_carry_the_sla_block()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee, "Bir");
        await TicketWorld.CreateIncidentAsync(world.Employee, "İki");

        var page = await TicketWorld.ReadAsync(world.Desk, TicketWorld.TicketsPath);

        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(2, item.GetProperty("sla").GetArrayLength());
        }
    }

    [Fact]
    public async Task Warned_instance_shows_in_detail_and_the_risk_queue_within_scope()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        Assert.Empty(await RiskAsync(world.Desk));
        factory.Clock.Advance(TimeSpan.FromMinutes(PastWarningMinutes));
        await MonitorAsync();

        Assert.Equal([TicketWorld.NumberOf(ticket)], await RiskAsync(world.Desk));
        Assert.Equal([TicketWorld.NumberOf(ticket)], await RiskAsync(world.Admin));
        Assert.Empty(await RiskAsync(world.Field));
        var detail = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket));
        Assert.True(Metric(detail, FirstResponse).GetProperty("isWarned").GetBoolean());
    }

    [Fact]
    public async Task Risk_queue_keeps_paused_and_breached_only_instances_but_not_stopped_ones()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var paused = await TicketWorld.CreateIncidentAsync(world.Employee, "Duraklatılmış");
        var breachedOnly = await TicketWorld.CreateIncidentAsync(world.Employee, "Yalnız ihlal");
        var stopped = await TicketWorld.CreateIncidentAsync(world.Employee, "Durmuş");
        var healthy = await TicketWorld.CreateIncidentAsync(world.Employee, "Sağlıklı");
        var now = factory.Clock.GetUtcNow();
        await using (var db = factory.Database.CreateDbContext())
        {
            var first = db.SlaInstances.Where(i => i.Metric == Tgysm.Domain.Sla.SlaMetric.FirstResponse);
            Task Update(JsonElement t, Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<Tgysm.Domain.Sla.SlaInstance>> set)
            {
                var number = TicketWorld.NumberOf(t);
                return first.Where(i => db.Tickets.Any(x => x.Id == i.TicketId && x.Number == number)).ExecuteUpdateAsync(set, Ct);
            }

            await Update(paused, s => s.SetProperty(i => i.WarnedAt, now).SetProperty(i => i.RunningSince, (DateTimeOffset?)null));
            await Update(breachedOnly, s => s.SetProperty(i => i.BreachedAt, now));
            await Update(stopped, s => s.SetProperty(i => i.WarnedAt, now).SetProperty(i => i.StoppedAt, now));
        }

        var risk = await RiskAsync(world.Desk);

        Assert.Equal(2, risk.Count);
        Assert.Contains(TicketWorld.NumberOf(paused), risk);
        Assert.Contains(TicketWorld.NumberOf(breachedOnly), risk);
        Assert.DoesNotContain(TicketWorld.NumberOf(stopped), risk);
        Assert.DoesNotContain(TicketWorld.NumberOf(healthy), risk);
    }

    [Fact]
    public async Task Risk_queue_is_forbidden_for_employees()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Employee.GetAsync($"{TicketWorld.TicketsPath}{SlaRiskQuery}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
