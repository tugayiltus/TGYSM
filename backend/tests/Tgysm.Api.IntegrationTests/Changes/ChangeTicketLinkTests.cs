using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeTicketLinkTests(ApiFactory factory) : IAsyncLifetime
{
    private const string TicketsSegment = "tickets";
    private const string ChangesSegment = "changes";
    private const string LinkExistsCode = "changes.ticket_link_exists";
    private const string LinkKindInvalidCode = "changes.ticket_link_kind_invalid";
    private const string LinkCancelledCode = "changes.ticket_link_cancelled";
    private const int ParallelAdds = 6;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> AddAsync(ApiClient client, JsonElement change, JsonElement ticket, string kind) =>
        client.PostAsync(ChangeWorld.PathOf(change, TicketsSegment), new { ticketNumber = TicketWorld.NumberOf(ticket), kind });

    private static async Task<JsonElement> LinkedAsync(ApiClient client, JsonElement change, JsonElement ticket, string kind)
    {
        using var response = await AddAsync(client, change, ticket, kind);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private async Task MoveToFieldTeamAsync(TicketWorld world, JsonElement ticket)
    {
        var id = ticket.GetProperty("id").GetGuid();
        await TestData.SaveAsync(factory, db => db.Tickets.Single(t => t.Id == id).Assign(world.FieldTeam.Id, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Owner_team_links_its_incident_as_caused_with_audit_timeline_and_no_version_bump()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var ticketBefore = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(incident));

        var list = await LinkedAsync(world.Desk, change, incident, "caused");

        var item = Assert.Single(list.EnumerateArray());
        Assert.Equal(TicketWorld.NumberOf(incident), item.GetProperty("number").GetString());
        Assert.Equal("caused", item.GetProperty("linkKind").GetString());
        Assert.Equal("Servis Masası", item.GetProperty("teamName").GetString());
        Assert.True(item.GetProperty("canOpen").GetBoolean());
        var listed = await TicketWorld.ReadAsync(world.Desk, ChangeWorld.PathOf(change, TicketsSegment));
        Assert.Equal(1, listed.GetArrayLength());

        var changeId = change.GetProperty("id").GetGuid();
        var ticketId = incident.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ChangeTicketLinked && a.EntityId == changeId, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketChangeLinked && a.EntityId == ticketId, Ct));
        Assert.Equal(1, await db.ChangeEvents.CountAsync(e => e.ChangeId == changeId && e.Kind == ChangeEventKind.TicketLinked, Ct));
        var ticketEvent = await db.TicketEvents.SingleAsync(e => e.TicketId == ticketId && e.Kind == TicketEventKind.ChangeLinked, Ct);
        Assert.Equal(Visibility.Internal, ticketEvent.Visibility);
        var changeAfter = await ChangeWorld.DetailAsync(world.Desk, change);
        Assert.Equal(ChangeWorld.VersionOf(change), ChangeWorld.VersionOf(changeAfter));
        var ticketAfter = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(incident));
        Assert.Equal(TicketWorld.VersionOf(ticketBefore), TicketWorld.VersionOf(ticketAfter));
    }

    [Fact]
    public async Task Other_teams_ticket_shows_the_change_summary_but_cannot_open_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        await MoveToFieldTeamAsync(world, incident);

        var list = await LinkedAsync(world.Desk, change, incident, "prompted");

        Assert.False(Assert.Single(list.EnumerateArray()).GetProperty("canOpen").GetBoolean());
        var changes = await TicketWorld.ReadAsync(world.Field, TicketWorld.PathOf(incident, ChangesSegment));
        var item = Assert.Single(changes.EnumerateArray());
        Assert.Equal(ChangeWorld.NumberOf(change), item.GetProperty("number").GetString());
        Assert.Equal("Sunucu güncellemesi", item.GetProperty("title").GetString());
        Assert.Equal("Servis Masası", item.GetProperty("ownerTeamName").GetString());
        Assert.False(item.GetProperty("canOpen").GetBoolean());
        using var open = await world.Field.GetAsync(ChangeWorld.PathOf(change));
        Assert.Equal(HttpStatusCode.NotFound, open.StatusCode);
    }

    [Fact]
    public async Task Same_pair_again_is_409_link_exists()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        await LinkedAsync(world.Desk, change, incident, "prompted");

        using var again = await AddAsync(world.Desk, change, incident, "caused");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(LinkExistsCode, await ApiClient.ReadCodeAsync(again));
    }

    [Fact]
    public async Task Parallel_adds_of_one_pair_give_one_201_and_the_rest_409_never_500()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        var responses = await Task.WhenAll(Enumerable.Range(0, ParallelAdds).Select(_ => AddAsync(world.Desk, change, incident, "prompted")));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        var conflictCodes = new List<string?>();
        foreach (var response in responses)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                conflictCodes.Add(await ApiClient.ReadCodeAsync(response));
            }

            response.Dispose();
        }

        Assert.Equal(1, statuses.Count(c => c == HttpStatusCode.Created));
        Assert.Equal(ParallelAdds - 1, statuses.Count(c => c == HttpStatusCode.Conflict));
        Assert.All(conflictCodes, code => Assert.Equal(LinkExistsCode, code));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.ChangeTicketLinks.CountAsync(Ct));
    }

    [Fact]
    public async Task Unique_pair_violation_in_the_database_maps_to_duplicate()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var changeId = change.GetProperty("id").GetGuid();
        var ticketId = incident.GetProperty("id").GetGuid();
        await TestData.SaveAsync(factory, db => db.ChangeTicketLinks.Add(
            ChangeTicketLink.Create(changeId, ticketId, TicketKind.Incident, ChangeTicketLinkKind.Prompted, world.DeskUser.Id, DateTimeOffset.UtcNow)));

        var duplicate = await Assert.ThrowsAsync<DomainException>(() => TestData.SaveAsync(factory, db => db.ChangeTicketLinks.Add(
            ChangeTicketLink.Create(changeId, ticketId, TicketKind.Incident, ChangeTicketLinkKind.Caused, world.DeskUser.Id, DateTimeOffset.UtcNow))));

        Assert.Equal(ErrorCodes.Duplicate, duplicate.Code);
    }

    [Fact]
    public async Task Caused_on_a_request_is_400()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var request = await world.CreateRequestAsync(world.Employee, approval: false);

        using var refused = await AddAsync(world.Desk, change, request, "caused");
        var prompted = await LinkedAsync(world.Desk, change, request, "prompted");

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(LinkKindInvalidCode, await ApiClient.ReadCodeAsync(refused));
        Assert.Equal(1, prompted.GetArrayLength());
    }

    [Fact]
    public async Task Cancelled_change_takes_no_link_and_closed_ticket_is_linkable()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var request = await world.CreateRequestAsync(world.Employee, approval: false);
        var requestId = request.GetProperty("id").GetGuid();
        await TestData.SaveAsync(factory, db => db.Tickets.Single(t => t.Id == requestId).Complete("Tamamlandı", null, false, true, DateTimeOffset.UtcNow));
        await LinkedAsync(world.Desk, change, request, "prompted");
        var changeId = change.GetProperty("id").GetGuid();
        await TestData.SaveAsync(factory, db => db.ChangeRequests.Single(c => c.Id == changeId).Cancel("Vazgeçildi", DateTimeOffset.UtcNow));
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var cancelled = await AddAsync(world.Desk, change, incident, "prompted");

        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
        Assert.Equal(LinkCancelledCode, await ApiClient.ReadCodeAsync(cancelled));
    }

    [Fact]
    public async Task Non_owner_staff_unknown_ticket_and_unknown_change_are_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var field = await AddAsync(world.Field, change, incident, "prompted");
        using var unknownTicket = await world.Desk.PostAsync(ChangeWorld.PathOf(change, TicketsSegment), new { ticketNumber = "INC-2026-999999", kind = "prompted" });
        using var unknownChange = await world.Desk.GetAsync($"{ChangeWorld.ChangesPath}/CHG-2026-999999/{TicketsSegment}");

        Assert.Equal(HttpStatusCode.NotFound, field.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownTicket.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownChange.StatusCode);
    }

    [Fact]
    public async Task Employee_requester_gets_404_on_ticket_changes_and_ticket_detail_has_no_link_field()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        await LinkedAsync(world.Desk, change, incident, "prompted");

        using var links = await world.Employee.GetAsync(TicketWorld.PathOf(incident, ChangesSegment));
        var detail = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(incident));
        var timeline = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(incident, "timeline"));

        Assert.Equal(HttpStatusCode.NotFound, links.StatusCode);
        Assert.DoesNotContain("change", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("change_linked", timeline.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_removes_the_link_and_records_unlink_audit_and_events()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        await LinkedAsync(world.Desk, change, incident, "prompted");

        using var removed = await world.Desk.DeleteAsync($"{ChangeWorld.PathOf(change, TicketsSegment)}/{TicketWorld.NumberOf(incident)}");

        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(0, (await world.Desk.ReadJsonAsync<JsonElement>(removed)).GetArrayLength());
        var changeId = change.GetProperty("id").GetGuid();
        var ticketId = incident.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ChangeTicketUnlinked && a.EntityId == changeId, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketChangeUnlinked && a.EntityId == ticketId, Ct));
        Assert.Equal(1, await db.ChangeEvents.CountAsync(e => e.ChangeId == changeId && e.Kind == ChangeEventKind.TicketUnlinked, Ct));
        Assert.Equal(1, await db.TicketEvents.CountAsync(e => e.TicketId == ticketId && e.Kind == TicketEventKind.ChangeUnlinked, Ct));
        using var again = await world.Desk.DeleteAsync($"{ChangeWorld.PathOf(change, TicketsSegment)}/{TicketWorld.NumberOf(incident)}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }
}
