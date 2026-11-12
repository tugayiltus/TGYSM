using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;

namespace Tgysm.Api.IntegrationTests.Tasks;

[Collection(ApiCollection.Name)]
public sealed class TaskListTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<List<string>> NumbersAsync(ApiClient client, string query = "")
    {
        var page = await TicketWorld.ReadAsync(client, $"{TaskWorld.TasksPath}{query}");
        return [.. page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("number").GetString()!)];
    }

    [Fact]
    public async Task Queues_split_my_team_and_unassigned_tasks()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var mine = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, world.FieldUser.Id);
        var open = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);
        var deskOnly = await TaskWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        Assert.Equal([TaskWorld.NumberOf(mine)], await NumbersAsync(world.Field, "?queue=mine"));
        Assert.Equal([TaskWorld.NumberOf(open), TaskWorld.NumberOf(mine)], await NumbersAsync(world.Field, "?queue=team"));
        Assert.Equal([TaskWorld.NumberOf(open)], await NumbersAsync(world.Field, "?queue=unassigned"));
        Assert.DoesNotContain(TaskWorld.NumberOf(deskOnly), await NumbersAsync(world.Field));
        Assert.Equal(3, (await NumbersAsync(world.Admin)).Count);
    }

    [Fact]
    public async Task Overdue_queue_lists_open_tasks_past_their_due_date()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var late = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, dueDate: "2020-01-01");
        await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, dueDate: "2999-01-01");
        var lateButClosed = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, dueDate: "2020-01-01");
        using (var cancel = await world.Field.PostAsync(TaskWorld.PathOf(lateButClosed, "cancel"), new { version = TaskWorld.VersionOf(lateButClosed), reason = "x" }))
        {
            cancel.EnsureSuccessStatusCode();
        }

        var page = await TicketWorld.ReadAsync(world.Field, $"{TaskWorld.TasksPath}?queue=overdue");

        var row = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(TaskWorld.NumberOf(late), row.GetProperty("number").GetString());
        Assert.True(row.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task Filters_and_search_narrow_the_list()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var backup = await TaskWorld.CreateAsync(world.Desk, new { title = "Gece yedeği", teamId = world.FieldTeam.Id, priority = "p1" });
        await TaskWorld.CreateAsync(world.Desk, new { title = "Kablo değişimi", teamId = world.FieldTeam.Id, priority = "p3" });

        Assert.Equal([TaskWorld.NumberOf(backup)], await NumbersAsync(world.Field, "?priority=p1"));
        Assert.Equal([TaskWorld.NumberOf(backup)], await NumbersAsync(world.Field, "?search=YEDEĞİ"));
        Assert.Equal([TaskWorld.NumberOf(backup)], await NumbersAsync(world.Field, $"?search={TaskWorld.NumberOf(backup)}"));
        Assert.Equal(2, (await NumbersAsync(world.Field, "?status=open")).Count);
    }

    [Fact]
    public async Task Employee_gets_404_on_the_list()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Employee.GetAsync(TaskWorld.TasksPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
