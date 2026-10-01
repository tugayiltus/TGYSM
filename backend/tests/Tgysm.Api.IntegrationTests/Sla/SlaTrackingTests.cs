using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Sla;

public sealed class SlaTrackingTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const int P3FirstResponseMinutes = 240;
    private const int P3IncidentResolutionMinutes = 3 * 540;
    private const int P1ResolutionMinutes = 240;
    private const int OtherTargetMinutes = 20;
    private const int SeedWarningPercent = 75;
    private const int SecondsPerMinute = 60;
    private const string PolicyPath = "/api/v1/admin/sla/policy";

    // Monday 10:00 in Istanbul (+03:00), inside the Mon-Fri 09:00-18:00 seed calendar. A FakeTimeProvider only moves
    // forward, so each test starts on the first such Monday after wherever the previous test left the clock.
    private static readonly DateTimeOffset First_start = new(2026, 10, 5, 7, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OneWeek = TimeSpan.FromDays(7);

    private DateTimeOffset _start;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        var start = First_start;
        while (start <= factory.Clock.GetUtcNow())
        {
            start += OneWeek;
        }

        factory.Clock.SetUtcNow(start);
        _start = start;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<JsonElement> OkAsync(ApiClient client, JsonElement ticket, string action, object? extra = null)
    {
        var body = new Dictionary<string, object?> { ["version"] = TicketWorld.VersionOf(ticket) };
        foreach (var property in extra?.GetType().GetProperties() ?? [])
        {
            body[property.Name] = property.GetValue(extra);
        }

        using var response = await client.PostAsync(TicketWorld.PathOf(ticket, action), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private async Task<SlaInstance> InstanceAsync(JsonElement ticket, SlaMetric metric)
    {
        await using var db = factory.Database.CreateDbContext();
        var id = await db.Tickets.Where(t => t.Number == TicketWorld.NumberOf(ticket)).Select(t => t.Id).SingleAsync(Ct);
        return await db.SlaInstances.AsNoTracking().Include(i => i.Pauses).Include(i => i.Events)
            .SingleAsync(i => i.TicketId == id && i.Metric == metric, Ct);
    }

    private async Task<HttpResponseMessage> CommentAsync(ApiClient client, JsonElement ticket, string visibility) =>
        await client.PostMultipartAsync(TicketWorld.PathOf(ticket, "comments"), new Dictionary<string, string> { ["body"] = "Merhaba", ["visibility"] = visibility }, []);

    [Fact]
    public async Task Incident_creates_two_running_instances_with_the_p3_targets()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        var first = await InstanceAsync(ticket, SlaMetric.FirstResponse);
        var resolution = await InstanceAsync(ticket, SlaMetric.Resolution);
        Assert.Equal((P3FirstResponseMinutes, SeedWarningPercent), (first.TargetMinutes, first.WarningPercent));
        Assert.Equal(P3IncidentResolutionMinutes, resolution.TargetMinutes);
        Assert.Equal(_start.AddHours(P3FirstResponseMinutes / 60), first.DueAt);
        Assert.NotNull(resolution.DueAt);
        Assert.All([first, resolution], i => Assert.Equal(_start, i.RunningSince));
    }

    [Fact]
    public async Task Request_needing_approval_starts_resolution_paused()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var request = await world.CreateRequestAsync(world.Employee, approval: true);

        var first = await InstanceAsync(request, SlaMetric.FirstResponse);
        var resolution = await InstanceAsync(request, SlaMetric.Resolution);
        Assert.False(first.IsPaused);
        Assert.True(resolution.IsPaused);
        Assert.Null(resolution.DueAt);
        Assert.Equal(HoldReason.ManagerApproval, Assert.Single(resolution.Pauses).HoldReason);
    }

    [Fact]
    public async Task Approval_resumes_resolution_and_rejection_cancels_both()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var approved = await world.CreateRequestAsync(world.Employee, approval: true);
        var rejected = await world.CreateRequestAsync(world.Employee, approval: true);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        string Path(JsonElement t, string d) => $"/api/v1/approvals/{t.GetProperty("approval").GetProperty("id").GetGuid()}/{d}";
        object Body(JsonElement t, string? reason) => new { version = t.GetProperty("approval").GetProperty("version").GetUInt32(), reason };
        using var approve = await world.Admin.PostAsync(Path(approved, "approve"), Body(approved, null));
        using var reject = await world.Admin.PostAsync(Path(rejected, "reject"), Body(rejected, "Bütçe yok"));

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var resumed = await InstanceAsync(approved, SlaMetric.Resolution);
        Assert.False(resumed.IsPaused);
        Assert.Equal(_start.AddHours(1), resumed.RunningSince);
        Assert.NotNull(resumed.DueAt);
        Assert.Equal(SlaResult.Cancelled, (await InstanceAsync(rejected, SlaMetric.Resolution)).Result);
        Assert.Equal(SlaResult.Cancelled, (await InstanceAsync(rejected, SlaMetric.FirstResponse)).Result);
    }

    [Fact]
    public async Task Invalidated_approval_pauses_resolution_again()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Employee, approval: true);
        using var approve = await world.Admin.PostAsync(
            $"/api/v1/approvals/{request.GetProperty("approval").GetProperty("id").GetGuid()}/approve",
            new { version = request.GetProperty("approval").GetProperty("version").GetUInt32(), reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var open = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(request));

        await OkAsync(world.Desk, open, "approval/invalidate", new { reason = "Kapsam değişti" });

        Assert.True((await InstanceAsync(request, SlaMetric.Resolution)).IsPaused);
        Assert.False((await InstanceAsync(request, SlaMetric.FirstResponse)).IsPaused);
    }

    [Fact]
    public async Task User_hold_pauses_resolution_but_not_first_response_and_resume_restarts_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        var held = await OkAsync(world.Desk, ticket, "hold", new { reason = "user" });

        var resolution = await InstanceAsync(ticket, SlaMetric.Resolution);
        Assert.True(resolution.IsPaused);
        Assert.Equal(3600, resolution.ElapsedSeconds);
        Assert.False((await InstanceAsync(ticket, SlaMetric.FirstResponse)).IsPaused);

        factory.Clock.Advance(TimeSpan.FromHours(2));
        await OkAsync(world.Desk, held, "resume");

        var resumed = await InstanceAsync(ticket, SlaMetric.Resolution);
        Assert.False(resumed.IsPaused);
        Assert.Equal(_start.AddHours(3), resumed.RunningSince);
        Assert.NotNull(Assert.Single(resumed.Pauses).EndedAt);
    }

    [Fact]
    public async Task Vendor_hold_pauses_nothing()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        await OkAsync(world.Desk, ticket, "hold", new { reason = "vendor" });

        Assert.All(Enum.GetValues<SlaMetric>(), metric => Assert.False(InstanceAsync(ticket, metric).GetAwaiter().GetResult().IsPaused));
    }

    [Fact]
    public async Task Transfer_and_claim_change_nothing()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var before = await InstanceAsync(ticket, SlaMetric.Resolution);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        var claimed = await OkAsync(world.Desk, ticket, "claim");
        await OkAsync(world.Desk, claimed, "transfer", new { teamId = world.FieldTeam.Id, assigneeId = world.FieldUser.Id, reason = "location", note = (string?)null });

        var after = await InstanceAsync(ticket, SlaMetric.Resolution);
        Assert.Equal((before.DueAt, before.RunningSince, before.ElapsedSeconds, before.Events.Count), (after.DueAt, after.RunningSince, after.ElapsedSeconds, after.Events.Count));
    }

    [Fact]
    public async Task First_it_public_reply_stops_first_response_as_met()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        using var reply = await CommentAsync(world.Desk, ticket, "public");

        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        var first = await InstanceAsync(ticket, SlaMetric.FirstResponse);
        Assert.Equal(SlaResult.Met, first.Result);
        Assert.Equal(_start.AddHours(1), first.StoppedAt);
        Assert.Null((await InstanceAsync(ticket, SlaMetric.Resolution)).Result);
    }

    [Fact]
    public async Task Requester_reply_and_internal_note_do_not_stop_first_response()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        using var requester = await CommentAsync(world.Employee, ticket, "public");
        using var note = await CommentAsync(world.Desk, ticket, "internal");

        Assert.Equal(HttpStatusCode.Created, requester.StatusCode);
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Null((await InstanceAsync(ticket, SlaMetric.FirstResponse)).StoppedAt);
    }

    [Fact]
    public async Task Priority_change_recomputes_due_from_the_original_start()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        factory.Clock.Advance(TimeSpan.FromHours(1));

        await OkAsync(world.Desk, ticket, "priority", new { impact = "high", urgency = "high", reason = "Acil" });

        var resolution = await InstanceAsync(ticket, SlaMetric.Resolution);
        Assert.Equal(P1ResolutionMinutes, resolution.TargetMinutes);
        Assert.Equal(_start.AddMinutes(P1ResolutionMinutes), resolution.DueAt);
        Assert.Equal(SecondsPerMinute * 60, resolution.ElapsedSeconds);
        Assert.Equal(_start, resolution.StartedAt);
        Assert.Contains(resolution.Events, e => e.Kind == SlaEventKind.TargetChanged);
    }

    [Fact]
    public async Task Complete_within_the_target_is_met_and_after_it_is_breached()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var onTime = await TicketWorld.CreateIncidentAsync(world.Employee);
        var late = await TicketWorld.CreateIncidentAsync(world.Employee);
        late = await OkAsync(world.Desk, late, "priority", new { impact = "high", urgency = "high", reason = "Acil" });
        factory.Clock.Advance(TimeSpan.FromHours(5));

        await OkAsync(world.Desk, onTime, "complete", new { resolution = "Tamam", outcome = "resolved", rootCause = (string?)null });
        await OkAsync(world.Desk, late, "complete", new { resolution = "Geç", outcome = "resolved", rootCause = (string?)null });

        Assert.Equal(SlaResult.Met, (await InstanceAsync(onTime, SlaMetric.Resolution)).Result);
        Assert.Equal(SlaResult.Breached, (await InstanceAsync(late, SlaMetric.Resolution)).Result);
        Assert.NotNull((await InstanceAsync(late, SlaMetric.FirstResponse)).StoppedAt);
    }

    [Fact]
    public async Task Cancel_stops_both_instances_as_cancelled()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        await OkAsync(world.Desk, ticket, "cancel", new { reason = "Vazgeçildi" });

        Assert.All(Enum.GetValues<SlaMetric>(), metric => Assert.Equal(SlaResult.Cancelled, InstanceAsync(ticket, metric).GetAwaiter().GetResult().Result));
    }

    [Fact]
    public async Task Policy_change_after_creation_does_not_change_existing_instances()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var targets = (from kind in new[] { "incident", "request" }
                       from priority in new[] { "p1", "p2", "p3", "p4" }
                       from metric in new[] { "first_response", "resolution" }
                       select (object)new { kind, priority, requestTypeId = (Guid?)null, metric, targetMinutes = OtherTargetMinutes, warningPercent = SeedWarningPercent }).ToList();
        using var publish = await world.Admin.PostAsync(PolicyPath, new { targets, pauseRules = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, publish.StatusCode);

        var held = await OkAsync(world.Desk, ticket, "hold", new { reason = "user" });
        await OkAsync(world.Desk, held, "resume");
        var current = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket));
        await OkAsync(world.Desk, current, "priority", new { impact = "high", urgency = "high", reason = "Acil" });

        var resolution = await InstanceAsync(ticket, SlaMetric.Resolution);
        Assert.Equal(P1ResolutionMinutes, resolution.TargetMinutes);
        Assert.Single(resolution.Pauses);
        var newTicket = await TicketWorld.CreateIncidentAsync(world.Employee);
        Assert.Equal(OtherTargetMinutes, (await InstanceAsync(newTicket, SlaMetric.Resolution)).TargetMinutes);
        Assert.NotEqual(resolution.PolicyVersionId, (await InstanceAsync(newTicket, SlaMetric.Resolution)).PolicyVersionId);
    }
}
