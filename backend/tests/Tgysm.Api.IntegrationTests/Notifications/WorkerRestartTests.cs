using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Notifications;
using Tgysm.Domain.Notifications;

namespace Tgysm.Api.IntegrationTests.Notifications;

/// <summary>
/// Acceptance criterion 12: the outbox event commits with its change or not at all, and work left behind by a worker or an SMTP
/// server that went away is picked up by the next run. Each job run uses its own scope, like a freshly started worker process.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WorkerRestartTests(ApiFactory factory) : IAsyncLifetime
{
    private const int Batch = 50;
    private const string FailingTrigger = "acceptance_fail_assigned_event";

    /// <summary>Refuses every <c>ticket_assigned</c> outbox insert, so the save that carries one fails inside PostgreSQL.</summary>
    private const string CreateFailingTriggerSql =
        "CREATE FUNCTION " + FailingTrigger + "() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN " +
        "IF NEW.event_type = '" + NotificationEventTypes.TicketAssigned + "' THEN RAISE EXCEPTION 'outbox insert refused'; END IF; " +
        "RETURN NEW; END $$; " +
        "CREATE TRIGGER " + FailingTrigger + " BEFORE INSERT ON notification_outbox FOR EACH ROW EXECUTE FUNCTION " + FailingTrigger + "();";

    private const string DropFailingTriggerSql =
        "DROP TRIGGER IF EXISTS " + FailingTrigger + " ON notification_outbox; DROP FUNCTION IF EXISTS " + FailingTrigger + "();";

    private const string ExpireOutboxLeasesSql = "UPDATE notification_outbox SET locked_until = now() - interval '1 hour'";
    private const string ExpireDeliveryBackOffSql = "UPDATE email_deliveries SET next_attempt_at = now() - interval '1 hour'";

    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.EmailSender.Reset();
    }

    public ValueTask DisposeAsync()
    {
        factory.EmailSender.Reset();
        return ValueTask.CompletedTask;
    }

    private async Task<int> DispatchAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(Batch, Lease, Ct);
    }

    private async Task<int> SendAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SendDueEmailsHandler>().HandleAsync(Batch, Lease, Ct);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var db = factory.Database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(sql, Ct);
    }

    [Fact]
    public async Task A_ticket_change_whose_outbox_event_cannot_be_written_is_rolled_back_with_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        await ExecuteAsync(CreateFailingTriggerSql);
        try
        {
            using var claim = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "claim"), new { version = TicketWorld.VersionOf(ticket) });
            Assert.False(claim.IsSuccessStatusCode);
        }
        finally
        {
            await ExecuteAsync(DropFailingTriggerSql);
        }

        var stored = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket));
        Assert.Equal(JsonValueKind.Null, stored.GetProperty("assigneeId").ValueKind);
        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.EventType == NotificationEventTypes.TicketAssigned, Ct));
        Assert.False(await db.TicketAssignments.AnyAsync(Ct));
    }

    [Fact]
    public async Task An_event_claimed_by_a_worker_that_died_is_dispatched_once_its_lease_expires()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await using (var deadWorker = factory.Services.CreateAsyncScope())
        {
            Assert.Single(await deadWorker.ServiceProvider.GetRequiredService<IOutboxClaimer>().ClaimAsync(Batch, Lease, Ct));
        }

        Assert.Equal(0, await DispatchAsync());
        await ExecuteAsync(ExpireOutboxLeasesSql);
        Assert.Equal(1, await DispatchAsync());
        Assert.Equal(0, await DispatchAsync());

        await SendAsync();
        await using var db = factory.Database.CreateDbContext();
        var single = await db.OutboxEvents.SingleAsync(Ct);
        Assert.Equal(OutboxStatus.Dispatched, single.Status);
        var delivery = await db.EmailDeliveries.SingleAsync(Ct);
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Single(factory.EmailSender.Sent);
    }

    [Fact]
    public async Task Emails_queued_while_smtp_is_down_are_sent_once_it_is_back()
    {
        var world = await TicketWorld.CreateAsync(factory);
        factory.EmailSender.FailWith = _ => new InvalidOperationException("smtp down");

        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        using var claim = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "claim"), new { version = TicketWorld.VersionOf(ticket) });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        await DispatchAsync();
        await SendAsync();

        await using (var during = factory.Database.CreateDbContext())
        {
            var queued = await during.EmailDeliveries.ToListAsync(Ct);
            Assert.NotEmpty(queued);
            Assert.All(queued, d => Assert.Equal(DeliveryStatus.Pending, d.Status));
            Assert.Empty(factory.EmailSender.Sent);
        }

        factory.EmailSender.FailWith = null;
        await ExecuteAsync(ExpireDeliveryBackOffSql);
        await SendAsync();

        await using var after = factory.Database.CreateDbContext();
        var deliveries = await after.EmailDeliveries.ToListAsync(Ct);
        Assert.All(deliveries, d => Assert.Equal(DeliveryStatus.Sent, d.Status));
        Assert.Equal(deliveries.Count, factory.EmailSender.Sent.Count);
    }
}
