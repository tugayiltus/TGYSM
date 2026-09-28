using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Notifications;

/// <summary>
/// Handlers write a transactional outbox event in the same save as their change (ADR-0015, Task 3). Dispatch and
/// email rendering are a later task; here only the write itself, its target ids and its atomicity are covered.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OutboxEmissionTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Guid IdOf(JsonElement ticket) => ticket.GetProperty("id").GetGuid();

    private static Guid? ActorIdOf(OutboxEvent outboxEvent)
    {
        using var payload = JsonDocument.Parse(outboxEvent.Payload);
        return payload.RootElement.TryGetProperty(OutboxEvent.ActorIdKey, out var value) ? value.GetGuid() : null;
    }

    private static async Task<JsonElement> ActionAsync(ApiClient client, JsonElement ticket, string action, object body)
    {
        using var response = await client.PostAsync(TicketWorld.PathOf(ticket, action), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static Task<HttpResponseMessage> CommentAsync(ApiClient client, JsonElement ticket, string body, string visibility) =>
        client.PostMultipartAsync(TicketWorld.PathOf(ticket, "comments"), new Dictionary<string, string> { ["body"] = body, ["visibility"] = visibility },
            Array.Empty<(string Name, byte[] Content, string ContentType)>());

    private async Task<List<OutboxEvent>> EventsOfTypeAsync(string eventType)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.OutboxEvents.Where(e => e.EventType == eventType).ToListAsync(Ct);
    }

    [Fact]
    public async Task Creating_an_incident_writes_exactly_one_ticket_created_event()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        var single = Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.TicketCreated));
        Assert.Equal(IdOf(ticket), single.TicketId);
        Assert.Null(single.UserId);
        Assert.Equal(OutboxStatus.Pending, single.Status);
    }

    [Fact]
    public async Task Claiming_writes_a_ticket_assigned_event_for_the_claimer()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });

        var single = Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.TicketAssigned));
        Assert.Equal(IdOf(ticket), single.TicketId);
        Assert.Equal(world.DeskUser.Id, single.UserId);
        Assert.Equal(world.DeskUser.Id, ActorIdOf(single));
    }

    [Fact]
    public async Task Transferring_writes_a_ticket_assigned_event_for_the_new_assignee()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        await ActionAsync(world.Desk, ticket, "transfer", new
        {
            version = TicketWorld.VersionOf(ticket),
            teamId = world.FieldTeam.Id,
            assigneeId = world.FieldUser.Id,
            reason = "location",
            note = (string?)null,
        });

        var single = Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.TicketAssigned));
        Assert.Equal(IdOf(ticket), single.TicketId);
        Assert.Equal(world.FieldUser.Id, single.UserId);
        Assert.Equal(world.DeskUser.Id, ActorIdOf(single));
    }

    [Fact]
    public async Task Public_reply_from_it_records_the_it_author()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });

        using var reply = await CommentAsync(world.Desk, ticket, "Yazıcıya bakıyorum", "public");

        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        var single = Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.PublicReply));
        Assert.Equal(IdOf(ticket), single.TicketId);
        Assert.Equal(world.DeskUser.Id, single.UserId);
        Assert.Equal(world.DeskUser.Id, ActorIdOf(single));
    }

    [Fact]
    public async Task Public_reply_from_the_requester_records_the_requester_as_author()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var reply = await CommentAsync(world.Employee, ticket, "Hâlâ bozuk", "public");

        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
        var single = Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.PublicReply));
        Assert.Equal(world.EmployeeUser.Id, single.UserId);
        Assert.Equal(world.EmployeeUser.Id, ActorIdOf(single));
    }

    [Fact]
    public async Task Internal_note_writes_no_event()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });

        using var note = await CommentAsync(world.Desk, ticket, "Depoda yok", "internal");

        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Empty(await EventsOfTypeAsync(NotificationEventTypes.PublicReply));
    }

    [Fact]
    public async Task Approval_requested_then_decided_write_events_for_the_approver_and_the_requester()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var request = await world.CreateRequestAsync(world.Employee, approval: true);

        var requested = Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.ApprovalRequested));
        Assert.Equal(IdOf(request), requested.TicketId);
        Assert.Equal(world.AdminUser.Id, requested.UserId);

        var approval = request.GetProperty("approval");
        using var approve = await world.Admin.PostAsync(
            $"/api/v1/approvals/{approval.GetProperty("id").GetGuid()}/approve",
            new { version = approval.GetProperty("version").GetUInt32() });

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var decided = Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.ApprovalDecided));
        Assert.Equal(IdOf(request), decided.TicketId);
        Assert.Null(decided.UserId);
    }

    [Fact]
    public async Task Completing_writes_a_ticket_completed_event_with_no_resolution_text_in_the_payload()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        ticket = await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });
        ticket = await ActionAsync(world.Desk, ticket, "start", new { version = TicketWorld.VersionOf(ticket) });
        const string resolutionText = "Toner değişti, yazıcı artık çalışıyor";

        using var complete = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "complete"),
            new { version = TicketWorld.VersionOf(ticket), resolution = resolutionText, outcome = "resolved" });

        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var single = Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.TicketCompleted));
        Assert.Equal(IdOf(ticket), single.TicketId);
        Assert.Null(single.UserId);
        Assert.DoesNotContain("Toner", single.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_claim_on_an_already_assigned_ticket_writes_no_extra_event()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (second, secondUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "desk2@example.com");
        await TestData.SaveAsync(factory, db => db.TeamMemberships.Add(Domain.Organization.TeamMembership.Create(world.ServiceDesk.Id, secondUser.Id, DateTimeOffset.UtcNow)));
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        ticket = await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });

        using var secondClaim = await second.PostAsync(TicketWorld.PathOf(ticket, "claim"), new { version = TicketWorld.VersionOf(ticket) });

        Assert.Equal(HttpStatusCode.Conflict, secondClaim.StatusCode);
        Assert.Single(await EventsOfTypeAsync(NotificationEventTypes.TicketAssigned));
    }
}
