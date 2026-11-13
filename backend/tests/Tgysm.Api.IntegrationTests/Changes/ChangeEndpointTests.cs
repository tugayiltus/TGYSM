using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ApprovalsPath = "/api/v1/change-approvals";
    private static readonly string[] WorkActions = ["submit", "withdraw", "start-implementation", "finish-implementation", "start-backout", "close", "cancel"];
    private static readonly string[] DecisionActions = ["approval/approve", "approval/reject", "approval/return"];
    private static readonly string[] PostActions = [.. WorkActions, .. DecisionActions];

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Anonymous_gets_401_on_every_change_route()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var anonymous = ApiClient.Create(factory);
        await anonymous.RefreshCsrfAsync();
        var path = ChangeWorld.PathOf(change);

        var statuses = new List<HttpStatusCode>();
        foreach (var url in new[] { ChangeWorld.ChangesPath, path, path + "/timeline", ApprovalsPath })
        {
            using var response = await anonymous.GetAsync(url);
            statuses.Add(response.StatusCode);
        }

        using (var create = await anonymous.PostAsync(ChangeWorld.ChangesPath, new { }))
        {
            statuses.Add(create.StatusCode);
        }

        using (var edit = await anonymous.PutAsync(path, new { }))
        {
            statuses.Add(edit.StatusCode);
        }

        foreach (var action in PostActions)
        {
            using var response = await anonymous.PostAsync($"{path}/{action}", new { version = 1 });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.Unauthorized, s));
    }

    [Fact]
    public async Task Employee_gets_404_on_every_change_route_and_an_empty_approvals_list()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var path = ChangeWorld.PathOf(change);

        var statuses = new List<HttpStatusCode>();
        foreach (var url in new[] { ChangeWorld.ChangesPath, path, path + "/timeline" })
        {
            using var response = await world.Employee.GetAsync(url);
            statuses.Add(response.StatusCode);
        }

        using (var edit = await world.Employee.PutAsync(path, new { version = ChangeWorld.VersionOf(change), draft = ChangeWorld.DraftBody(), assetIds = Array.Empty<Guid>() }))
        {
            statuses.Add(edit.StatusCode);
        }

        foreach (var action in WorkActions)
        {
            using var response = await world.Employee.PostAsync($"{path}/{action}", new { version = ChangeWorld.VersionOf(change), reason = "x" });
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.NotFound, s));

        // Deciding is admin-only, so a non-admin is refused the same way whatever the number.
        foreach (var action in DecisionActions)
        {
            using var response = await world.Employee.PostAsync($"{path}/{action}", new { approvalVersion = 1, reason = "x" });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        using var approvals = await world.Employee.GetAsync(ApprovalsPath);
        Assert.Equal(HttpStatusCode.OK, approvals.StatusCode);
        Assert.Equal(0, (await world.Employee.ReadJsonAsync<JsonElement>(approvals)).GetArrayLength());
    }

    [Fact]
    public async Task Timeline_is_404_for_summary_access_and_open_to_full_access()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);
        var path = ChangeWorld.PathOf(change);

        using var summary = await world.Field.GetAsync(path);
        using var timelineAsSummary = await world.Field.GetAsync(path + "/timeline");
        using var timelineAsOwner = await world.Desk.GetAsync(path + "/timeline");

        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, timelineAsSummary.StatusCode);
        Assert.Equal(HttpStatusCode.OK, timelineAsOwner.StatusCode);
    }

    [Fact]
    public async Task Approvals_list_is_empty_for_staff_who_are_not_admins()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Desk.GetAsync(ApprovalsPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await world.Desk.ReadJsonAsync<JsonElement>(response)).GetArrayLength());
    }
}
