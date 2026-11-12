using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tasks;
using Tgysm.Api.IntegrationTests.Tickets;

namespace Tgysm.Api.IntegrationTests.Work;

[Collection(ApiCollection.Name)]
public sealed class WorkListTests(ApiFactory factory) : IAsyncLifetime
{
    private const string WorkItemsPath = "/api/v1/work-items";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<JsonElement> PageAsync(ApiClient client, string query) =>
        await TicketWorld.ReadAsync(client, $"{WorkItemsPath}{query}");

    private static List<(string Kind, string Number)> Rows(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(r => (r.GetProperty("kind").GetString()!, r.GetProperty("number").GetString()!))];

    [Fact]
    public async Task Assigned_to_me_combines_tickets_and_tasks_newest_first()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        using (var claim = await world.Desk.PostAsync(TicketWorld.PathOf(incident, "claim"), new { version = TicketWorld.VersionOf(incident) }))
        {
            claim.EnsureSuccessStatusCode();
        }

        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, world.DeskUser.Id);

        var rows = Rows(await PageAsync(world.Desk, "?queue=assigned_to_me"));

        Assert.Equal([("task", TaskWorld.NumberOf(task)), ("incident", TicketWorld.NumberOf(incident))], rows);
    }

    [Fact]
    public async Task My_teams_lists_open_items_of_my_teams_and_pages_across_both_kinds()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await TaskWorld.CreateForTeamAsync(world.Field, world.ServiceDesk.Id);
        await TaskWorld.CreateForTeamAsync(world.Field, world.ServiceDesk.Id);
        await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);

        var first = await PageAsync(world.Desk, "?queue=my_teams&page=1&pageSize=2");
        var second = await PageAsync(world.Desk, "?queue=my_teams&page=2&pageSize=2");

        Assert.Equal(3, first.GetProperty("totalCount").GetInt32());
        var all = Rows(first).Concat(Rows(second)).ToList();
        Assert.Equal(3, all.Distinct().Count());
        Assert.Equal(1, all.Count(r => r.Kind == "incident"));
        Assert.All(all.Where(r => r.Kind == "task"), r => Assert.StartsWith("TSK-", r.Number, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Closed_items_drop_out()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, world.DeskUser.Id);
        using (var cancel = await world.Desk.PostAsync(TaskWorld.PathOf(task, "cancel"), new { version = TaskWorld.VersionOf(task), reason = "x" }))
        {
            cancel.EnsureSuccessStatusCode();
        }

        Assert.Empty(Rows(await PageAsync(world.Desk, "?queue=assigned_to_me")));
    }

    [Fact]
    public async Task Row_carries_title_status_team_and_assignee()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TaskWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, world.DeskUser.Id, dueDate: "2026-12-01");

        var row = (await PageAsync(world.Desk, "?queue=assigned_to_me")).GetProperty("items")[0];

        Assert.Equal("Yedek al", row.GetProperty("title").GetString());
        Assert.Equal("open", row.GetProperty("status").GetString());
        Assert.Equal("p2", row.GetProperty("priority").GetString());
        Assert.Equal("2026-12-01", row.GetProperty("dueDate").GetString());
        Assert.Equal("Servis Masası", row.GetProperty("teamName").GetString());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("assigneeName").GetString()));
    }

    [Fact]
    public async Task Employee_gets_404()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Employee.GetAsync($"{WorkItemsPath}?queue=assigned_to_me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
