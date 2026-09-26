using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class AuditAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string AuditPath = "/api/v1/admin/audit-events";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Lists_newest_first_with_actor_name_and_filters_by_entity_type()
    {
        var (admin, self) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        using var department = await admin.PostAsync("/api/v1/admin/departments", new { name = "Finans" });
        using var team = await admin.PostAsync("/api/v1/admin/teams", new { name = "Masaüstü Destek" });

        using var all = await admin.GetAsync(AuditPath);
        using var teamsOnly = await admin.GetAsync($"{AuditPath}?entityType={AuditEntityTypes.Team}");

        var allItems = (await admin.ReadJsonAsync<JsonElement>(all)).GetProperty("items");
        Assert.Equal(AuditActions.TeamCreated, allItems[0].GetProperty("action").GetString());
        Assert.Equal("Test User", allItems[0].GetProperty("actorName").GetString());
        Assert.Equal(self.Id, allItems[0].GetProperty("actorUserId").GetGuid());
        var teamItems = (await admin.ReadJsonAsync<JsonElement>(teamsOnly)).GetProperty("items");
        Assert.All(teamItems.EnumerateArray(), item => Assert.Equal(AuditEntityTypes.Team, item.GetProperty("entityType").GetString()));
    }

    [Fact]
    public async Task Future_from_date_returns_nothing()
    {
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

        using var response = await admin.GetAsync($"{AuditPath}?from={from}");

        Assert.Equal(0, (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Non_admin_is_forbidden()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var response = await client.GetAsync(AuditPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
