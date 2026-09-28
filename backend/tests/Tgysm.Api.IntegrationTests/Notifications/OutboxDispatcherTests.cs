using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Notifications;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Notifications;

/// <summary>
/// <see cref="DispatchOutboxHandler"/> against real ticket flows (ADR-0014, ADR-0015, spec §3.2–3.3): recipient
/// rules, template rendering scoped to allowed variables, the skip path for no active recipient, and safety under
/// two concurrent dispatcher runs.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OutboxDispatcherTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<System.Text.Json.JsonElement> ActionAsync(ApiClient client, System.Text.Json.JsonElement ticket, string action, object body)
    {
        using var response = await client.PostAsync(TicketWorld.PathOf(ticket, action), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<System.Text.Json.JsonElement>(response);
    }

    private static Task<HttpResponseMessage> CommentAsync(ApiClient client, System.Text.Json.JsonElement ticket, string body, string visibility) =>
        client.PostMultipartAsync(TicketWorld.PathOf(ticket, "comments"), new Dictionary<string, string> { ["body"] = body, ["visibility"] = visibility },
            Array.Empty<(string Name, byte[] Content, string ContentType)>());

    private async Task<int> DispatchAsync(int batch = 50)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>();
        return await handler.HandleAsync(batch, Lease, Ct);
    }

    private async Task<EmailDelivery> SingleDeliveryForAsync(string eventType, Guid recipientUserId)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.EmailDeliveries.SingleAsync(d => d.TemplateType == eventType && d.RecipientUserId == recipientUserId, Ct);
    }

    private async Task<List<Guid>> RecipientIdsForAsync(string eventType)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.EmailDeliveries.Where(d => d.TemplateType == eventType).Select(d => d.RecipientUserId).ToListAsync(Ct);
    }

    [Fact]
    public async Task Completion_event_renders_resolution_but_not_internal_note_or_root_cause()
    {
        const string internalNoteText = "Depoda yedek toner yok, başka şubeden isteniyor";
        const string resolutionText = "Toner değişti, yazıcı artık çalışıyor";
        const string rootCauseText = "Toner kartuşu ömrünü doldurmuş";

        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        ticket = await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });
        ticket = await ActionAsync(world.Desk, ticket, "start", new { version = TicketWorld.VersionOf(ticket) });

        using var note = await CommentAsync(world.Desk, ticket, internalNoteText, "internal");
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        // A comment bumps the ticket's xmin (it touches updated_at) without going through the version check
        // (CLAUDE.md), so the cached ticket JSON's version is now stale; re-read before the next versioned action.
        ticket = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket));

        using var complete = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "complete"),
            new { version = TicketWorld.VersionOf(ticket), resolution = resolutionText, outcome = "resolved", rootCause = rootCauseText });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        await DispatchAsync();

        var delivery = await SingleDeliveryForAsync(NotificationEventTypes.TicketCompleted, world.EmployeeUser.Id);
        Assert.NotNull(delivery.Body);
        Assert.Contains(resolutionText, delivery.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(rootCauseText, delivery.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(internalNoteText, delivery.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_reply_by_the_requester_goes_to_the_assignee_not_the_requester()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        ticket = await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });

        using var reply = await CommentAsync(world.Employee, ticket, "Hâlâ bozuk", "public");
        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);

        await DispatchAsync();

        var recipients = await RecipientIdsForAsync(NotificationEventTypes.PublicReply);
        Assert.Contains(world.DeskUser.Id, recipients);
        Assert.DoesNotContain(world.EmployeeUser.Id, recipients);
    }

    [Fact]
    public async Task Transfer_to_a_team_only_notifies_its_active_members_not_the_actor()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (_, secondFieldUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "field2@example.com");
        await TestData.SaveAsync(factory, db => db.TeamMemberships.Add(TeamMembership.Create(world.FieldTeam.Id, secondFieldUser.Id, DateTimeOffset.UtcNow)));

        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        ticket = await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });

        ticket = await ActionAsync(world.Desk, ticket, "transfer", new
        {
            version = TicketWorld.VersionOf(ticket),
            teamId = world.FieldTeam.Id,
            assigneeId = (Guid?)null,
            reason = "location",
            note = (string?)null,
        });

        await DispatchAsync();

        var recipients = await RecipientIdsForAsync(NotificationEventTypes.TicketAssigned);
        Assert.Contains(world.FieldUser.Id, recipients);
        Assert.Contains(secondFieldUser.Id, recipients);
        Assert.DoesNotContain(world.DeskUser.Id, recipients);
    }

    [Fact]
    public async Task Inactive_requester_leaves_the_event_skipped_with_no_delivery()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await TestData.SaveAsync(factory, db => db.Users.Single(u => u.Id == world.EmployeeUser.Id).Deactivate(DateTimeOffset.UtcNow));

        await DispatchAsync();

        await using var db = factory.Database.CreateDbContext();
        var outboxEvent = await db.OutboxEvents.SingleAsync(e => e.EventType == NotificationEventTypes.TicketCreated, Ct);
        Assert.Equal(OutboxStatus.Skipped, outboxEvent.Status);
        Assert.False(await db.EmailDeliveries.AnyAsync(d => d.OutboxId == outboxEvent.Id, Ct));
    }

    [Fact]
    public async Task Two_parallel_dispatch_runs_dispatch_each_event_once_with_no_duplicate_delivery()
    {
        const int ticketCount = 10;
        var world = await TicketWorld.CreateAsync(factory);
        for (var i = 0; i < ticketCount; i++)
        {
            await TicketWorld.CreateIncidentAsync(world.Employee, subject: $"Yazıcı arızası {i}");
        }

        var results = await Task.WhenAll(DispatchAsync(ticketCount), DispatchAsync(ticketCount));

        Assert.Equal(ticketCount, results.Sum());
        var deliveries = await RecipientIdsForAsync(NotificationEventTypes.TicketCreated);
        Assert.Equal(ticketCount, deliveries.Count);

        await using var db = factory.Database.CreateDbContext();
        var distinctOutboxIds = await db.EmailDeliveries
            .Where(d => d.TemplateType == NotificationEventTypes.TicketCreated)
            .Select(d => d.OutboxId).Distinct().CountAsync(Ct);
        Assert.Equal(ticketCount, distinctOutboxIds);
    }
}
