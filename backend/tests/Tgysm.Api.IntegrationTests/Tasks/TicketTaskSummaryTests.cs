using System.Net;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;

namespace Tgysm.Api.IntegrationTests.Tasks;

[Collection(ApiCollection.Name)]
public sealed class TicketTaskSummaryTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Ticket_detail_lists_its_tasks_and_counts_open_ones()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var open = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, ticketNumber: TicketWorld.NumberOf(incident));
        var closed = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, ticketNumber: TicketWorld.NumberOf(incident));
        using (var cancel = await world.Field.PostAsync(TaskWorld.PathOf(closed, "cancel"), new { version = TaskWorld.VersionOf(closed), reason = "x" }))
        {
            cancel.EnsureSuccessStatusCode();
        }

        var detail = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(incident));

        Assert.Equal(1, detail.GetProperty("openTaskCount").GetInt32());
        var tasks = detail.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("number").GetString()).ToList();
        Assert.Equal([TaskWorld.NumberOf(open), TaskWorld.NumberOf(closed)], tasks);
    }

    [Fact]
    public async Task Requester_does_not_see_tasks()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, ticketNumber: TicketWorld.NumberOf(incident));

        var detail = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(incident));

        Assert.Equal(0, detail.GetProperty("openTaskCount").GetInt32());
        Assert.Empty(detail.GetProperty("tasks").EnumerateArray());
    }

    [Fact]
    public async Task Completing_a_ticket_with_open_tasks_is_allowed()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, ticketNumber: TicketWorld.NumberOf(incident));
        var current = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(incident));

        using var response = await world.Desk.PostAsync(TicketWorld.PathOf(incident, "complete"), new { version = TicketWorld.VersionOf(current), resolution = "Çözüldü", outcome = "resolved" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
