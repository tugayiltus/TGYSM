using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class TeamAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string TeamsPath = "/api/v1/admin/teams";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static async Task<JsonElement> CreateTeamAsync(ApiClient admin, string name)
    {
        using var response = await admin.PostAsync(TeamsPath, new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    private static Guid Id(JsonElement item) => item.GetProperty("id").GetGuid();

    [Fact]
    public async Task Adds_and_removes_members_and_counts_them_in_the_list()
    {
        var admin = await AdminAsync();
        var team = await CreateTeamAsync(admin, "Saha Destek");
        var itStaff = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "tech@example.com");
        var otherAdmin = await TestUsers.CreateAsync(factory, UserRole.Admin, "lead@example.com");

        using var addFirst = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = itStaff.Id });
        using var addSecond = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = otherAdmin.Id });
        using var list = await admin.GetAsync(TeamsPath);
        Assert.Equal(2, (await admin.ReadJsonAsync<JsonElement>(list)).GetProperty("items")[0].GetProperty("memberCount").GetInt32());

        using var remove = await admin.DeleteAsync($"{TeamsPath}/{Id(team)}/members/{itStaff.Id}");
        var detail = await admin.ReadJsonAsync<JsonElement>(remove);
        Assert.Equal(1, detail.GetProperty("members").GetArrayLength());
        Assert.Equal(otherAdmin.Id, detail.GetProperty("members")[0].GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task Member_rules_are_enforced()
    {
        var admin = await AdminAsync();
        var team = await CreateTeamAsync(admin, "Ağ Ekibi");
        var employee = await TestUsers.CreateAsync(factory, UserRole.Employee, "employee@example.com");
        var tech = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "tech@example.com");
        var inactiveTech = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "gone@example.com");
        await using (var db = factory.Database.CreateDbContext())
        {
            var tracked = await db.Users.FindAsync([inactiveTech.Id], TestContext.Current.CancellationToken);
            tracked!.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var employeeAdd = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = employee.Id });
        using var inactiveAdd = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = inactiveTech.Id });
        using var firstAdd = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = tech.Id });
        using var duplicateAdd = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = tech.Id });
        using var removeUnknown = await admin.DeleteAsync($"{TeamsPath}/{Id(team)}/members/{employee.Id}");

        Assert.Equal(ErrorCodes.MemberRoleInvalid, await ApiClient.ReadCodeAsync(employeeAdd));
        Assert.Equal(ErrorCodes.MemberInactive, await ApiClient.ReadCodeAsync(inactiveAdd));
        Assert.Equal(HttpStatusCode.OK, firstAdd.StatusCode);
        Assert.Equal(ErrorCodes.MembershipExists, await ApiClient.ReadCodeAsync(duplicateAdd));
        Assert.Equal(HttpStatusCode.NotFound, removeUnknown.StatusCode);
    }

    [Fact]
    public async Task Inactive_team_rejects_new_members_and_names_are_unique()
    {
        var admin = await AdminAsync();
        var team = await CreateTeamAsync(admin, "Yazılım");
        var tech = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "tech@example.com");

        using var deactivate = await admin.PostAsync($"{TeamsPath}/{Id(team)}/deactivate", new { version = team.GetProperty("version").GetUInt32() });
        using var add = await admin.PostAsync($"{TeamsPath}/{Id(team)}/members", new { userId = tech.Id });
        using var duplicateName = await admin.PostAsync(TeamsPath, new { name = "YAZILIM" });

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(add));
        Assert.Equal(ErrorCodes.TeamNameTaken, await ApiClient.ReadCodeAsync(duplicateName));
    }

    [Fact]
    public async Task Rename_with_stale_version_returns_conflict()
    {
        var admin = await AdminAsync();
        var team = await CreateTeamAsync(admin, "Destek");
        var version = team.GetProperty("version").GetUInt32();

        using var first = await admin.PutAsync($"{TeamsPath}/{Id(team)}", new { version, name = "Destek 1" });
        using var second = await admin.PutAsync($"{TeamsPath}/{Id(team)}", new { version, name = "Destek 2" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }
}
