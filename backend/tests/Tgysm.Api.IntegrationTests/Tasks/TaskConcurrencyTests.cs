using System.Net;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Tasks;

[Collection(ApiCollection.Name)]
public sealed class TaskConcurrencyTests(ApiFactory factory) : IAsyncLifetime
{
    private const int Attempts = 5;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Parallel_claims_have_one_winner()
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            await factory.ResetAsync();
            var world = await TicketWorld.CreateAsync(factory);
            var (second, secondUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "field2@example.com");
            await TestData.SaveAsync(factory, db => db.TeamMemberships.Add(Tgysm.Domain.Organization.TeamMembership.Create(world.FieldTeam.Id, secondUser.Id, DateTimeOffset.UtcNow)));
            var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);
            var body = new { version = TaskWorld.VersionOf(task) };

            var responses = await Task.WhenAll(world.Field.PostAsync(TaskWorld.PathOf(task, "claim"), body), second.PostAsync(TaskWorld.PathOf(task, "claim"), body));

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(r => r.StatusCode).Order());
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Claim_with_a_stale_version_is_a_conflict_and_assigns_no_one()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id);
        using (var start = await world.Field.PostAsync(TaskWorld.PathOf(task, "start"), new { version = TaskWorld.VersionOf(task) }))
        {
            start.EnsureSuccessStatusCode();
        }

        using var response = await world.Field.PostAsync(TaskWorld.PathOf(task, "claim"), new { version = TaskWorld.VersionOf(task) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var current = await TicketWorld.ReadAsync(world.Field, TaskWorld.PathOf(task));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, current.GetProperty("assigneeId").ValueKind);
    }

    [Fact]
    public async Task Claim_of_an_assigned_task_is_refused()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, world.FieldUser.Id);

        using var response = await world.Field.PostAsync(TaskWorld.PathOf(task, "claim"), new { version = TaskWorld.VersionOf(task) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("tasks.already_claimed", await ApiClient.ReadCodeAsync(response));
    }
}
