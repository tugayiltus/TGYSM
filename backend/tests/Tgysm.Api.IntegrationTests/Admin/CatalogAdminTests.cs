using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class CatalogAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string CategoriesPath = "/api/v1/admin/categories";
    private const string RequestTypesPath = "/api/v1/admin/request-types";
    private const string TeamsPath = "/api/v1/admin/teams";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static async Task<JsonElement> CreatedAsync(ApiClient client, string path, object body)
    {
        using var response = await client.PostAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static Guid IdOf(JsonElement item) => item.GetProperty("id").GetGuid();

    private static uint VersionOf(JsonElement item) => item.GetProperty("version").GetUInt32();

    [Fact]
    public async Task Categories_have_at_most_two_levels()
    {
        var admin = await AdminAsync();
        var hardware = await CreatedAsync(admin, CategoriesPath, new { name = "Donanım", parentId = (Guid?)null, scope = "both" });
        var printer = await CreatedAsync(admin, CategoriesPath, new { name = "Yazıcı", parentId = IdOf(hardware), scope = "incident" });
        var software = await CreatedAsync(admin, CategoriesPath, new { name = "Yazılım", parentId = (Guid?)null, scope = "both" });

        using var third = await admin.PostAsync(CategoriesPath, new { name = "Toner", parentId = IdOf(printer), scope = "incident" });
        using var moveParent = await admin.PutAsync($"{CategoriesPath}/{IdOf(hardware)}", new { version = VersionOf(hardware), name = "Donanım", parentId = IdOf(software), scope = "both" });

        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        Assert.Equal(ErrorCodes.CategoryTooDeep, await ApiClient.ReadCodeAsync(third));
        Assert.Equal(ErrorCodes.CategoryTooDeep, await ApiClient.ReadCodeAsync(moveParent));
    }

    [Fact]
    public async Task Names_are_unique_ignoring_turkish_case()
    {
        var admin = await AdminAsync();
        await CreatedAsync(admin, CategoriesPath, new { name = "Işık", parentId = (Guid?)null, scope = "both" });
        await CreatedAsync(admin, RequestTypesPath, new { name = "Yeni cihaz", requiresApproval = true });

        using var category = await admin.PostAsync(CategoriesPath, new { name = "IŞIK", parentId = (Guid?)null, scope = "both" });
        using var requestType = await admin.PostAsync(RequestTypesPath, new { name = "YENİ CİHAZ", requiresApproval = false });

        Assert.Equal(ErrorCodes.CategoryNameTaken, await ApiClient.ReadCodeAsync(category));
        Assert.Equal(ErrorCodes.RequestTypeNameTaken, await ApiClient.ReadCodeAsync(requestType));
    }

    [Fact]
    public async Task Reference_lists_show_active_entries_for_the_ticket_kind()
    {
        var admin = await AdminAsync();
        var hardware = await CreatedAsync(admin, CategoriesPath, new { name = "Donanım", parentId = (Guid?)null, scope = "both" });
        await CreatedAsync(admin, CategoriesPath, new { name = "Arıza", parentId = IdOf(hardware), scope = "incident" });
        await CreatedAsync(admin, CategoriesPath, new { name = "Kurulum", parentId = IdOf(hardware), scope = "request" });
        var old = await CreatedAsync(admin, CategoriesPath, new { name = "Eski", parentId = (Guid?)null, scope = "both" });
        using var _ = await admin.PostAsync($"{CategoriesPath}/{IdOf(old)}/deactivate", new { version = VersionOf(old) });
        await CreatedAsync(admin, RequestTypesPath, new { name = "Yeni cihaz", requiresApproval = true });
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "e@example.com");

        using var incidentCategories = await employee.GetAsync("/api/v1/reference/categories?kind=incident");
        using var requestTypes = await employee.GetAsync("/api/v1/reference/request-types");

        var paths = (await employee.ReadJsonAsync<JsonElement>(incidentCategories)).EnumerateArray().Select(c => c.GetProperty("path").GetString());
        Assert.Equal(["Donanım", "Donanım / Arıza"], paths);
        var types = (await employee.ReadJsonAsync<JsonElement>(requestTypes)).EnumerateArray().ToList();
        Assert.True(Assert.Single(types).GetProperty("requiresApproval").GetBoolean());
    }

    [Fact]
    public async Task Intake_flag_moves_from_one_team_to_another()
    {
        var admin = await AdminAsync();
        var first = await CreatedAsync(admin, TeamsPath, new { name = "Servis Masası" });
        var second = await CreatedAsync(admin, TeamsPath, new { name = "Saha Destek" });

        using var setFirst = await admin.PutAsync($"{TeamsPath}/{IdOf(first)}", new { version = VersionOf(first), name = "Servis Masası", isDefaultIntake = true });
        using var setSecond = await admin.PutAsync($"{TeamsPath}/{IdOf(second)}", new { version = VersionOf(second), name = "Saha Destek", isDefaultIntake = true });

        Assert.Equal(HttpStatusCode.OK, setFirst.StatusCode);
        Assert.True((await admin.ReadJsonAsync<JsonElement>(setSecond)).GetProperty("isDefaultIntake").GetBoolean());
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(["Saha Destek"], await db.Teams.Where(t => t.IsDefaultIntake).Select(t => t.Name).ToListAsync(Ct));
    }

    [Fact]
    public async Task Employee_and_it_staff_cannot_manage_the_catalog()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var response = await it.PostAsync(RequestTypesPath, new { name = "X", requiresApproval = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
