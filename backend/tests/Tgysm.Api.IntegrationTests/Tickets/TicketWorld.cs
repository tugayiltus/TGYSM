using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Tickets;

/// <summary>
/// A small organisation for ticket tests: service desk (intake, member "desk") and field team (member "field"),
/// an admin who approves the "Muhasebe" department, and two employees in it.
/// </summary>
public sealed class TicketWorld
{
    public const string TicketsPath = "/api/v1/tickets";
    public const string IncidentsPath = TicketsPath + "/incidents";
    public const string RequestsPath = TicketsPath + "/requests";

    private TicketWorld()
    {
    }

    public ApiClient Employee { get; private set; } = null!;
    public ApiClient OtherEmployee { get; private set; } = null!;
    public ApiClient Desk { get; private set; } = null!;
    public ApiClient Field { get; private set; } = null!;
    public ApiClient Admin { get; private set; } = null!;
    public User EmployeeUser { get; private set; } = null!;
    public User DeskUser { get; private set; } = null!;
    public User FieldUser { get; private set; } = null!;
    public User AdminUser { get; private set; } = null!;
    public Team ServiceDesk { get; private set; } = null!;
    public Team FieldTeam { get; private set; } = null!;
    public Department Accounting { get; private set; } = null!;
    public RequestType NewLaptop { get; private set; } = null!;
    public RequestType Software { get; private set; } = null!;
    public Category Hardware { get; private set; } = null!;
    public Location Office { get; private set; } = null!;
    public ApiFactory Factory { get; private set; } = null!;

    public static async Task<TicketWorld> CreateAsync(ApiFactory factory, bool withIntakeTeam = true)
    {
        var world = new TicketWorld { Factory = factory };
        (world.Admin, world.AdminUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        world.Accounting = await TestData.DepartmentAsync(factory, "Muhasebe", world.AdminUser.Id);
        world.Office = await TestData.LocationAsync(factory, "Merkez");
        world.EmployeeUser = await TestUsers.CreateAsync(factory, UserRole.Employee, "ayse@example.com", departmentId: world.Accounting.Id, firstName: "Ayşe", lastName: "Kaya");
        world.Employee = ApiClient.Create(factory);
        await world.Employee.LoginAsync("ayse@example.com");
        await TestUsers.CreateAsync(factory, UserRole.Employee, "ali@example.com", departmentId: world.Accounting.Id, firstName: "Ali", lastName: "Veli");
        world.OtherEmployee = ApiClient.Create(factory);
        await world.OtherEmployee.LoginAsync("ali@example.com");
        (world.Desk, world.DeskUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "desk@example.com");
        (world.Field, world.FieldUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "field@example.com");
        world.ServiceDesk = await TestData.TeamAsync(factory, "Servis Masası", withIntakeTeam, world.DeskUser.Id);
        world.FieldTeam = await TestData.TeamAsync(factory, "Saha Destek", false, world.FieldUser.Id);
        world.NewLaptop = await TestData.RequestTypeAsync(factory, "Yeni dizüstü", requiresApproval: true);
        world.Software = await TestData.RequestTypeAsync(factory, "Yazılım kurulumu", requiresApproval: false);
        world.Hardware = await TestData.CategoryAsync(factory, "Donanım");
        return world;
    }

    public static async Task<JsonElement> CreateIncidentAsync(ApiClient client, string subject = "Yazıcı çalışmıyor", object? extra = null)
    {
        using var response = await client.PostAsync(IncidentsPath, extra ?? new { subject, description = "Kağıt sıkışıyor" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    public async Task<JsonElement> CreateRequestAsync(ApiClient client, bool approval)
    {
        using var response = await client.PostAsync(RequestsPath, new { subject = "Yeni cihaz", description = "Göreve başlıyorum", requestTypeId = approval ? NewLaptop.Id : Software.Id });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    public static string PathOf(JsonElement ticket, string? action = null) =>
        action is null ? $"{TicketsPath}/{NumberOf(ticket)}" : $"{TicketsPath}/{NumberOf(ticket)}/{action}";

    public static string NumberOf(JsonElement ticket) => ticket.GetProperty("number").GetString()!;

    public static uint VersionOf(JsonElement ticket) => ticket.GetProperty("version").GetUInt32();

    public static async Task<JsonElement> ReadAsync(ApiClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }
}
