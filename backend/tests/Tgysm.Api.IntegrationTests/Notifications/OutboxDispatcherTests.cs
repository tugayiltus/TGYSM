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

    private async Task<OutboxEvent> OutboxEventAsync(Guid id)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.OutboxEvents.SingleAsync(e => e.Id == id, Ct);
    }

    private static Guid IdOf(System.Text.Json.JsonElement ticket) => ticket.GetProperty("id").GetGuid();

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

    [Fact]
    public async Task A_bad_event_backs_off_without_blocking_the_rest_of_the_batch()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var badEventId = Guid.Empty;

        // An unknown event type makes RecipientResolver throw — stands in for any dispatch-time failure (a
        // deleted ticket, a bad App:TimeZone, ...) without needing to corrupt otherwise-valid data.
        await TestData.SaveAsync(factory, db =>
        {
            var bad = OutboxEvent.Create("not_a_real_event_type", null, null, "{}", null, DateTimeOffset.UtcNow);
            badEventId = bad.Id;
            db.OutboxEvents.Add(bad);
        });

        // Created after the bad one, so RowLease claims the bad one first (ORDER BY id, and Guid.CreateVersion7
        // is time-ordered) — exactly the ordering that let one bad event starve everything behind it before this fix.
        await TicketWorld.CreateIncidentAsync(world.Employee);

        await DispatchAsync();

        var goodDelivery = await SingleDeliveryForAsync(NotificationEventTypes.TicketCreated, world.EmployeeUser.Id);
        Assert.NotNull(goodDelivery);
        // Not just "a delivery row exists": the good event's own status must have actually reached the database.
        // Recovering from the bad event's failure must not silently drop this event's own not-yet-saved change.
        var goodOutboxEvent = await OutboxEventAsync(goodDelivery.OutboxId);
        Assert.Equal(OutboxStatus.Dispatched, goodOutboxEvent.Status);

        var bad = await OutboxEventAsync(badEventId);
        Assert.Equal(OutboxStatus.Pending, bad.Status);
        Assert.Equal(1, bad.Attempts);
        Assert.NotNull(bad.LockedUntil);
        Assert.True(bad.LockedUntil > DateTimeOffset.UtcNow);
        Assert.False(string.IsNullOrWhiteSpace(bad.LastError));
    }

    [Fact]
    public async Task Completion_email_for_a_maximum_length_subject_is_dispatched_with_a_truncated_subject()
    {
        // "[{{ticketNumber}}] {{subject}} tamamlandı" around a 200-character ticket subject renders past the
        // email_deliveries.subject column (varchar 200); before the fix the save threw 22001 on every run.
        var longSubject = new string('a', Domain.Tickets.Ticket.SubjectMaxLength);
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee, subject: longSubject);
        ticket = await ActionAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });
        ticket = await ActionAsync(world.Desk, ticket, "start", new { version = TicketWorld.VersionOf(ticket) });
        await ActionAsync(world.Desk, ticket, "complete",
            new { version = TicketWorld.VersionOf(ticket), resolution = "Toner değişti", outcome = "resolved", rootCause = (string?)null });

        await DispatchAsync();

        var delivery = await SingleDeliveryForAsync(NotificationEventTypes.TicketCompleted, world.EmployeeUser.Id);
        Assert.Equal(EmailDelivery.SubjectMaxLength, delivery.Subject.Length);
        Assert.StartsWith($"[{TicketWorld.NumberOf(ticket)}] ", delivery.Subject, StringComparison.Ordinal);
        var outboxEvent = await OutboxEventAsync(delivery.OutboxId);
        Assert.Equal(OutboxStatus.Dispatched, outboxEvent.Status);
    }

    [Fact]
    public async Task An_event_whose_save_throws_is_recorded_as_failed_from_a_fresh_scope_and_the_batch_continues()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee, subject: "Birinci");

        // A delivery that already exists for (event, recipient) makes the dispatch save hit the unique index —
        // a save-time failure, unlike an unknown event type, so the failed scope still tracks the added delivery.
        Guid poisonId;
        await using (var db = factory.Database.CreateDbContext())
        {
            poisonId = await db.OutboxEvents.Where(e => e.EventType == NotificationEventTypes.TicketCreated).Select(e => e.Id).SingleAsync(Ct);
        }

        await TestData.SaveAsync(factory, db => db.EmailDeliveries.Add(EmailDelivery.Create(
            poisonId, new Recipient(world.EmployeeUser.Id, world.EmployeeUser.Email, "Çalışan"), NotificationEventTypes.TicketCreated, "konu", "gövde", DateTimeOffset.UtcNow)));

        var second = await TicketWorld.CreateIncidentAsync(world.Employee, subject: "İkinci");

        await DispatchAsync();

        var poison = await OutboxEventAsync(poisonId);
        Assert.Equal(OutboxStatus.Pending, poison.Status);
        Assert.Equal(1, poison.Attempts);
        Assert.False(string.IsNullOrWhiteSpace(poison.LastError));

        await using var check = factory.Database.CreateDbContext();
        var secondEvent = await check.OutboxEvents.SingleAsync(e => e.EventType == NotificationEventTypes.TicketCreated && e.TicketId == IdOf(second), Ct);
        Assert.Equal(OutboxStatus.Dispatched, secondEvent.Status);
    }

    [Fact]
    public async Task After_five_failed_attempts_a_bad_event_becomes_failed()
    {
        var badEventId = Guid.Empty;
        await TestData.SaveAsync(factory, db =>
        {
            var bad = OutboxEvent.Create("not_a_real_event_type", null, null, "{}", null, DateTimeOffset.UtcNow);
            badEventId = bad.Id;
            db.OutboxEvents.Add(bad);
        });

        for (var attempt = 1; attempt <= OutboxEvent.MaxAttempts; attempt++)
        {
            await DispatchAsync();
            var current = await OutboxEventAsync(badEventId);
            Assert.Equal(attempt, current.Attempts);

            if (attempt < OutboxEvent.MaxAttempts)
            {
                Assert.Equal(OutboxStatus.Pending, current.Status);
                Assert.NotNull(current.LockedUntil);

                // Simulate the back-off having elapsed instead of waiting on it for real (same trick RowLeaseTests
                // uses for an expired lease), so the next DispatchAsync() can reclaim it.
                await using var db = factory.Database.CreateDbContext();
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE notification_outbox SET locked_until = now() - interval '1 hour' WHERE id = {current.Id}", Ct);
            }
            else
            {
                Assert.Equal(OutboxStatus.Failed, current.Status);
                Assert.Null(current.LockedUntil);
            }
        }
    }

    [Fact]
    public async Task Event_without_an_actor_id_excludes_nobody()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        // ticket_assigned with no actorId in the payload — a Worker-emitted event has no signed-in HTTP actor, so
        // there is nobody to exclude and the assignee must still be notified (spec §3.3's exclusion is "the actor",
        // not "the assignee"; resolved ambiguity: absent actorId excludes nobody).
        await TestData.SaveAsync(factory, db =>
            db.OutboxEvents.Add(OutboxEvent.Create(NotificationEventTypes.TicketAssigned, IdOf(ticket), world.DeskUser.Id, "{}", null, DateTimeOffset.UtcNow)));

        await DispatchAsync();

        var recipients = await RecipientIdsForAsync(NotificationEventTypes.TicketAssigned);
        Assert.Contains(world.DeskUser.Id, recipients);
    }

    [Fact]
    public async Task Account_event_delivery_has_no_body_and_no_secret_anywhere()
    {
        var world = await TicketWorld.CreateAsync(factory);
        const string secret = "super-secret-activation-token";

        await TestData.SaveAsync(factory, db =>
            db.OutboxEvents.Add(OutboxEvent.Create(NotificationEventTypes.AccountActivation, null, world.EmployeeUser.Id, "{}", secret, DateTimeOffset.UtcNow)));

        await DispatchAsync();

        var delivery = await SingleDeliveryForAsync(NotificationEventTypes.AccountActivation, world.EmployeeUser.Id);
        Assert.Null(delivery.Body);
        Assert.DoesNotContain(secret, delivery.Subject, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sla_event_on_an_unassigned_ticket_goes_to_active_team_members()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        await TestData.SaveAsync(factory, db =>
            db.OutboxEvents.Add(OutboxEvent.Create(NotificationEventTypes.SlaWarning, IdOf(ticket), null, "{}", null, DateTimeOffset.UtcNow)));

        await DispatchAsync();

        var recipients = await RecipientIdsForAsync(NotificationEventTypes.SlaWarning);
        Assert.Contains(world.DeskUser.Id, recipients);
    }

    [Fact]
    public async Task Status_variable_renders_the_turkish_label()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);

        await DispatchAsync();

        var delivery = await SingleDeliveryForAsync(NotificationEventTypes.TicketCreated, world.EmployeeUser.Id);
        Assert.NotNull(delivery.Body);
        Assert.Contains("Açık", delivery.Body, StringComparison.Ordinal);
    }
}
