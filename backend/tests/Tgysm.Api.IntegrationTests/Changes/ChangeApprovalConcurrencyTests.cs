using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeApprovalConcurrencyTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Two_admins_approving_in_parallel_give_one_200_one_409_and_one_approved_row()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (second, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin2@example.com");
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);
        using var submit = await world.Desk.PostAsync(ChangeWorld.PathOf(change, "submit"), new { version = ChangeWorld.VersionOf(change) });
        var submitted = await world.Desk.ReadJsonAsync<JsonElement>(submit);
        var body = new { approvalVersion = submitted.GetProperty("approval").GetProperty("version").GetUInt32() };
        var path = ChangeWorld.PathOf(submitted, "approval/approve");

        var responses = await Task.WhenAll(world.Admin.PostAsync(path, body), second.PostAsync(path, body));

        try
        {
            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
            var loser = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("changes.approval_not_pending", await ApiClient.ReadCodeAsync(loser));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var id = submitted.GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.ChangeApprovals.Where(a => a.ChangeId == id).ToListAsync(Ct);
        Assert.Equal(ChangeApprovalStatus.Approved, Assert.Single(rows).Status);
        Assert.Equal(ChangeStatus.Scheduled, (await db.ChangeRequests.SingleAsync(c => c.Id == id, Ct)).Status);
    }

    [Fact]
    public async Task Decision_with_a_stale_approval_version_while_still_pending_is_a_concurrency_conflict()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);
        using var submit = await world.Desk.PostAsync(ChangeWorld.PathOf(change, "submit"), new { version = ChangeWorld.VersionOf(change) });
        var submitted = await world.Desk.ReadJsonAsync<JsonElement>(submit);
        var version = submitted.GetProperty("approval").GetProperty("version").GetUInt32();

        using var response = await world.Admin.PostAsync(ChangeWorld.PathOf(submitted, "approval/approve"), new { approvalVersion = version + 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("concurrency.conflict", await ApiClient.ReadCodeAsync(response));
    }
}
