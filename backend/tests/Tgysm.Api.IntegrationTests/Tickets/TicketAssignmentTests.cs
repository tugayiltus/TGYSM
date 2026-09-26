using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class TicketAssignmentTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task It_staff_claims_a_ticket_of_their_team()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var claim = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "claim"), new { version = TicketWorld.VersionOf(ticket) });
        var claimed = await world.Desk.ReadJsonAsync<JsonElement>(claim);

        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        Assert.Equal(world.DeskUser.Id, claimed.GetProperty("assigneeId").GetGuid());
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.TicketAssignments.CountAsync(Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.TicketAssigned, Ct));
    }

    [Fact]
    public async Task Other_team_and_employees_cannot_claim()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var field = await world.Field.PostAsync(TicketWorld.PathOf(ticket, "claim"), new { version = TicketWorld.VersionOf(ticket) });
        using var employee = await world.Employee.PostAsync(TicketWorld.PathOf(ticket, "claim"), new { version = TicketWorld.VersionOf(ticket) });

        Assert.Equal(HttpStatusCode.NotFound, field.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, employee.StatusCode);
    }

    [Fact]
    public async Task Concurrent_claims_have_one_winner()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (second, secondUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "desk2@example.com");
        await TestData.SaveAsync(factory, db => db.TeamMemberships.Add(Domain.Organization.TeamMembership.Create(world.ServiceDesk.Id, secondUser.Id, DateTimeOffset.UtcNow)));
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var body = new { version = TicketWorld.VersionOf(ticket) };

        var responses = await Task.WhenAll(world.Desk.PostAsync(TicketWorld.PathOf(ticket, "claim"), body), second.PostAsync(TicketWorld.PathOf(ticket, "claim"), body));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.TicketAssignments.CountAsync(Ct));
    }

    [Fact]
    public async Task Transfer_moves_the_ticket_to_another_team_member()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var transfer = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "transfer"), new
        {
            version = TicketWorld.VersionOf(ticket),
            teamId = world.FieldTeam.Id,
            assigneeId = world.FieldUser.Id,
            reason = "location",
            note = (string?)null,
        });
        var fieldView = await TicketWorld.ReadAsync(world.Field, TicketWorld.PathOf(ticket));
        using var deskView = await world.Desk.GetAsync(TicketWorld.PathOf(ticket));

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Equal("Saha Destek", fieldView.GetProperty("teamName").GetString());
        Assert.Equal(world.FieldUser.Id, fieldView.GetProperty("assigneeId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, deskView.StatusCode);
    }

    [Fact]
    public async Task Transfer_rules_reason_note_and_team_membership()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var path = TicketWorld.PathOf(ticket, "transfer");
        var version = TicketWorld.VersionOf(ticket);

        using var noNote = await world.Desk.PostAsync(path, new { version, teamId = world.FieldTeam.Id, assigneeId = (Guid?)null, reason = "other", note = " " });
        using var notMember = await world.Desk.PostAsync(path, new { version, teamId = world.FieldTeam.Id, assigneeId = world.DeskUser.Id, reason = "workload", note = (string?)null });
        using var badReason = await world.Desk.PostAsync(path, new { version, teamId = world.FieldTeam.Id, reason = "bored" });

        Assert.Contains(ErrorCodes.Required, (await ApiClient.ReadFieldCodesAsync(noNote))["note"]);
        Assert.Equal(ErrorCodes.TicketAssigneeNotInTeam, await ApiClient.ReadCodeAsync(notMember));
        Assert.Equal(HttpStatusCode.BadRequest, badReason.StatusCode);
    }
}
