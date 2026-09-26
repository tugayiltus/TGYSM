using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class TicketStatusTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<JsonElement> OkAsync(ApiClient client, JsonElement ticket, string action, object body)
    {
        using var response = await client.PostAsync(TicketWorld.PathOf(ticket, action), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Ticket_moves_through_start_hold_resume_and_complete()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        ticket = await OkAsync(world.Desk, ticket, "claim", new { version = TicketWorld.VersionOf(ticket) });
        ticket = await OkAsync(world.Desk, ticket, "start", new { version = TicketWorld.VersionOf(ticket) });
        ticket = await OkAsync(world.Desk, ticket, "hold", new { version = TicketWorld.VersionOf(ticket), reason = "vendor", note = (string?)null });
        Assert.Equal(("on_hold", "vendor"), (ticket.GetProperty("status").GetString(), ticket.GetProperty("holdReason").GetString()));
        ticket = await OkAsync(world.Desk, ticket, "resume", new { version = TicketWorld.VersionOf(ticket) });
        Assert.Equal("in_progress", ticket.GetProperty("status").GetString());
        ticket = await OkAsync(world.Desk, ticket, "complete", new { version = TicketWorld.VersionOf(ticket), resolution = "Toner değişti", outcome = "resolved", rootCause = "Eski toner" });

        Assert.Equal("completed", ticket.GetProperty("status").GetString());
        Assert.Equal("Toner değişti", ticket.GetProperty("resolution").GetString());
        var employeeView = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(ticket));
        Assert.Equal("completed", employeeView.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, employeeView.GetProperty("rootCause").ValueKind);
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.TicketCompleted, Ct));
    }

    [Fact]
    public async Task Hold_needs_a_reason_and_complete_needs_a_resolution()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var version = TicketWorld.VersionOf(ticket);

        using var noReason = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "hold"), new { version });
        using var otherNoNote = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "hold"), new { version, reason = "other" });
        using var noResolution = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "complete"), new { version, resolution = " ", outcome = "resolved" });
        using var noOutcome = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "complete"), new { version, resolution = "Tamam" });

        Assert.Contains(ErrorCodes.Required, (await ApiClient.ReadFieldCodesAsync(noReason))["reason"]);
        Assert.Equal(ErrorCodes.TicketHoldNoteRequired, await ApiClient.ReadCodeAsync(otherNoNote));
        Assert.Contains(ErrorCodes.Required, (await ApiClient.ReadFieldCodesAsync(noResolution))["resolution"]);
        Assert.Equal(ErrorCodes.TicketOutcomeRequired, await ApiClient.ReadCodeAsync(noOutcome));
    }

    [Fact]
    public async Task Employee_cannot_change_status_or_priority()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var version = TicketWorld.VersionOf(ticket);

        using var cancel = await world.Employee.PostAsync(TicketWorld.PathOf(ticket, "cancel"), new { version, reason = "Vazgeçtim" });
        using var complete = await world.Employee.PostAsync(TicketWorld.PathOf(ticket, "complete"), new { version, resolution = "Oldu", outcome = "resolved" });
        using var priority = await world.Employee.PostAsync(TicketWorld.PathOf(ticket, "priority"), new { version, impact = "high", urgency = "high", reason = "Acil" });

        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, priority.StatusCode);
    }

    [Fact]
    public async Task Priority_change_needs_a_reason_and_records_old_and_new()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var noReason = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "priority"), new { version = TicketWorld.VersionOf(ticket), impact = "high", urgency = "high", reason = "" });
        var changed = await OkAsync(world.Desk, ticket, "priority", new { version = TicketWorld.VersionOf(ticket), impact = "high", urgency = "medium", reason = "Tüm kat etkileniyor" });

        Assert.Contains(ErrorCodes.Required, (await ApiClient.ReadFieldCodesAsync(noReason))["reason"]);
        Assert.Equal("p2", changed.GetProperty("priority").GetString());
        await using var db = factory.Database.CreateDbContext();
        var audit = await db.AuditEvents.SingleAsync(a => a.Action == AuditActions.TicketPriorityChanged, Ct);
        Assert.Contains("p3", audit.OldValues!, StringComparison.Ordinal);
        Assert.Contains("p2", audit.NewValues!, StringComparison.Ordinal);
        Assert.Equal("Tüm kat etkileniyor", audit.Reason);
    }

    [Fact]
    public async Task Closed_tickets_refuse_status_changes_and_stale_versions_conflict()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var cancelled = await OkAsync(world.Desk, ticket, "cancel", new { version = TicketWorld.VersionOf(ticket), reason = "Mükerrer" });

        using var start = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "start"), new { version = TicketWorld.VersionOf(cancelled) });
        using var stale = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "start"), new { version = TicketWorld.VersionOf(ticket) });

        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(ErrorCodes.TicketInvalidTransition, await ApiClient.ReadCodeAsync(start));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }
}
