using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Notifications;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeWorkflowTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Start_implementation_while_pending_approval_is_an_invalid_transition()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.FieldTeam.Id);
        var submitted = await ChangeWorld.ActAsync(world.Desk, await ChangeWorld.DetailAsync(world.Desk, change), "submit");

        using var response = await world.Desk.PostAsync(ChangeWorld.PathOf(submitted, "start-implementation"), new { version = ChangeWorld.VersionOf(submitted) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.invalid_transition", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Scheduled_change_runs_implementing_then_verifying_with_history()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);

        var implementing = await ChangeWorld.ActAsync(world.Desk, scheduled, "start-implementation");
        Assert.Equal("implementing", implementing.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, implementing.GetProperty("actualStart").ValueKind);

        var verifying = await ChangeWorld.ActAsync(world.Desk, implementing, "finish-implementation");
        Assert.Equal("verifying", verifying.GetProperty("status").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = scheduled.GetProperty("id").GetGuid();
        var kinds = await db.ChangeEvents.Where(e => e.ChangeId == id).Select(e => e.Kind).ToListAsync(Ct);
        Assert.Contains(ChangeEventKind.ImplementationStarted, kinds);
        Assert.Contains(ChangeEventKind.ImplementationFinished, kinds);
    }

    [Fact]
    public async Task Start_backout_marks_the_change_and_asks_the_backout_workers_once()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);
        var implementing = await ChangeWorld.ActAsync(world.Desk, scheduled, "start-implementation");

        var backout = await ChangeWorld.ActAsync(world.Desk, implementing, "start-backout");

        Assert.Equal("implementing", backout.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, backout.GetProperty("backoutStartedAt").ValueKind);
        using var again = await world.Desk.PostAsync(ChangeWorld.PathOf(backout, "start-backout"), new { version = ChangeWorld.VersionOf(backout) });
        Assert.Equal("changes.invalid_transition", await ApiClient.ReadCodeAsync(again));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mail = await db.OutboxEvents.SingleAsync(e => e.EventType == NotificationEventTypes.ChangeBackoutStarted, Ct);
        Assert.Equal(OutboxSubjectTypes.Change, mail.SubjectType);
        Assert.Equal(backout.GetProperty("id").GetGuid(), mail.SubjectId);
    }

    [Fact]
    public async Task Workflow_actions_need_the_owner_team()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);

        using var response = await world.Field.PostAsync(ChangeWorld.PathOf(scheduled, "start-implementation"), new { version = ChangeWorld.VersionOf(scheduled) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stale_version_is_a_concurrency_conflict()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (scheduled, _) = await ChangeWorld.ScheduledAsync(world);

        using var response = await world.Desk.PostAsync(ChangeWorld.PathOf(scheduled, "start-implementation"), new { version = ChangeWorld.VersionOf(scheduled) + 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("concurrency.conflict", await ApiClient.ReadCodeAsync(response));
    }
}
