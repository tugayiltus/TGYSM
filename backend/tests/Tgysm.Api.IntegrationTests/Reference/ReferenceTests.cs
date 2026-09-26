using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Reference;

[Collection(ApiCollection.Name)]
public sealed class ReferenceTests(ApiFactory factory) : IAsyncLifetime
{
    private const string LocationsPath = "/api/v1/reference/locations";
    private const string DepartmentsPath = "/api/v1/reference/departments";
    private const string UsersPath = "/api/v1/reference/users";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<List<JsonElement>> ListAsync(ApiClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await client.ReadJsonAsync<JsonElement>(response)).EnumerateArray()];
    }

    [Fact]
    public async Task Locations_are_active_only_with_full_paths()
    {
        var campus = await TestData.LocationAsync(factory, "Merkez Kampüs");
        await TestData.LocationAsync(factory, "A Blok", campus.Id);
        await TestData.LocationAsync(factory, "Eski Depo", active: false);
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "e@example.com");

        var locations = await ListAsync(employee, LocationsPath);

        Assert.Equal(["Merkez Kampüs", "Merkez Kampüs / A Blok"], locations.Select(l => l.GetProperty("path").GetString()));
    }

    [Fact]
    public async Task Departments_are_active_only()
    {
        await TestData.DepartmentAsync(factory, "Muhasebe");
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "e@example.com");

        var departments = await ListAsync(employee, DepartmentsPath);

        Assert.Equal(["Muhasebe"], departments.Select(d => d.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task It_staff_finds_active_users_by_name_but_employees_cannot()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "ayse@example.com");
        var inactive = await TestUsers.CreateAsync(factory, UserRole.Employee, "pasif@example.com");
        await TestData.SaveAsync(factory, db => db.Users.Attach(inactive).Entity.Deactivate(DateTimeOffset.UtcNow));

        var found = await ListAsync(it, $"{UsersPath}?search=ayse");
        var none = await ListAsync(it, $"{UsersPath}?search=pasif");
        using var forbidden = await employee.GetAsync($"{UsersPath}?search=it");

        Assert.Equal(["ayse@example.com"], found.Select(u => u.GetProperty("email").GetString()));
        Assert.Empty(none);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task It_staff_lists_active_teams_with_active_it_members()
    {
        var (it, itUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        await TestData.TeamAsync(factory, "Saha Destek", false, itUser.Id);
        var old = await TestData.TeamAsync(factory, "Eski Ekip");
        await TestData.SaveAsync(factory, db => db.Teams.Attach(old).Entity.Deactivate());
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "e@example.com");

        var teams = await ListAsync(it, "/api/v1/reference/teams");
        using var forbidden = await employee.GetAsync("/api/v1/reference/teams");

        var team = Assert.Single(teams);
        Assert.Equal("Saha Destek", team.GetProperty("name").GetString());
        Assert.Equal(itUser.Id, team.GetProperty("members")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
