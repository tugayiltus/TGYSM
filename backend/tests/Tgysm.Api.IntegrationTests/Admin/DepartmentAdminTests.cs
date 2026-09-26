using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class DepartmentAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string DepartmentsPath = "/api/v1/admin/departments";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static async Task<JsonElement> CreateAsync(ApiClient admin, string name, Guid? approverUserId = null)
    {
        using var response = await admin.PostAsync(DepartmentsPath, new { name, approverUserId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Create_list_and_rename()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, "Bilgi İşlem");

        using var renamed = await admin.PutAsync($"{DepartmentsPath}/{created.GetProperty("id").GetGuid()}", new
        {
            version = created.GetProperty("version").GetUInt32(),
            name = "Bilgi Teknolojileri",
            approverUserId = (Guid?)null,
        });
        using var list = await admin.GetAsync($"{DepartmentsPath}?search={Uri.EscapeDataString("BİLGİ")}");

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var items = (await admin.ReadJsonAsync<JsonElement>(list)).GetProperty("items");
        Assert.Equal("Bilgi Teknolojileri", items[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Department_name_is_unique_ignoring_turkish_case()
    {
        var admin = await AdminAsync();
        await CreateAsync(admin, "İnsan Kaynakları");

        using var duplicate = await admin.PostAsync(DepartmentsPath, new { name = "insan kaynakları" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ErrorCodes.DepartmentNameTaken, await ApiClient.ReadCodeAsync(duplicate));
    }

    [Fact]
    public async Task Approver_must_be_an_active_admin()
    {
        var admin = await AdminAsync();
        var employee = await TestUsers.CreateAsync(factory, UserRole.Employee, "employee@example.com");
        var approver = await TestUsers.CreateAsync(factory, UserRole.Admin, "approver@example.com", firstName: "Onay", lastName: "Veren");

        using var rejected = await admin.PostAsync(DepartmentsPath, new { name = "Satış", approverUserId = employee.Id });
        var accepted = await CreateAsync(admin, "Satış", approver.Id);

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(ErrorCodes.ApproverInvalid, await ApiClient.ReadCodeAsync(rejected));
        Assert.Equal("Onay Veren", accepted.GetProperty("approverName").GetString());
    }

    [Fact]
    public async Task Stale_version_returns_conflict()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, "Hukuk");
        var path = $"{DepartmentsPath}/{created.GetProperty("id").GetGuid()}";
        var version = created.GetProperty("version").GetUInt32();

        using var first = await admin.PutAsync(path, new { version, name = "Hukuk Müşavirliği" });
        using var second = await admin.PutAsync(path, new { version, name = "Hukuk Birimi" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task Deactivate_and_reactivate_are_audited()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, "Lojistik");
        var id = created.GetProperty("id").GetGuid();

        using var deactivated = await admin.PostAsync($"{DepartmentsPath}/{id}/deactivate", new { version = created.GetProperty("version").GetUInt32() });
        var afterDeactivate = await admin.ReadJsonAsync<JsonElement>(deactivated);
        using var reactivated = await admin.PostAsync($"{DepartmentsPath}/{id}/reactivate", new { version = afterDeactivate.GetProperty("version").GetUInt32() });

        Assert.False(afterDeactivate.GetProperty("isActive").GetBoolean());
        Assert.True((await admin.ReadJsonAsync<JsonElement>(reactivated)).GetProperty("isActive").GetBoolean());
        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents.Where(a => a.EntityId == id).Select(a => a.Action).ToListAsync(Ct);
        Assert.Contains(AuditActions.DepartmentCreated, actions);
        Assert.Contains(AuditActions.DepartmentDeactivated, actions);
        Assert.Contains(AuditActions.DepartmentReactivated, actions);
    }

    [Fact]
    public async Task Non_admin_is_forbidden()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var response = await client.PostAsync(DepartmentsPath, new { name = "Gizli" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ascii_capital_i_does_not_create_a_second_department()
    {
        var admin = await AdminAsync();
        await CreateAsync(admin, "IT Destek");

        using var duplicate = await admin.PostAsync(DepartmentsPath, new { name = "it destek" });

        Assert.Equal(ErrorCodes.DepartmentNameTaken, await ApiClient.ReadCodeAsync(duplicate));
    }
}
