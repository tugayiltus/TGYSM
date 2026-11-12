using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Notifications;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>Testing, switching and the rules that follow the active login method (ADR-0030).</summary>
[Collection(ApiCollection.Name)]
public sealed class LoginMethodSwitchTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SettingsPath = "/api/v1/admin/auth-settings";
    private const string Password = TestUsers.DefaultPassword;
    private const string LdapPassword = "AdŞifresi-2026";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.LdapDirectory.Reset();
        factory.LdapDirectory.Add("ahmet.yilmaz", "ahmet.yilmaz@sirket.com", LdapPassword);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<(ApiClient Client, User User)> AdminAsync() => ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "yonetici@sirket.com");

    private static async Task<uint> VersionAsync(ApiClient client)
    {
        using var get = await client.GetAsync(SettingsPath);
        return (await client.ReadJsonAsync<JsonElement>(get)).GetProperty("version").GetUInt32();
    }

    private static async Task SaveLdapAsync(ApiClient admin, string host = "dc1.sirket.local")
    {
        using var put = await admin.PutAsync($"{SettingsPath}/ldap", new { host, port = 636, security = "ldaps", baseDn = "DC=sirket,DC=local", bindDn = "CN=svc,DC=sirket,DC=local", bindPassword = "svc", userFilter = (string?)null, version = await VersionAsync(admin) });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
    }

    private static async Task<HttpResponseMessage> SwitchAsync(ApiClient admin, string method) =>
        await admin.PostAsync($"{SettingsPath}/method", new { method, version = await VersionAsync(admin) });

    private static Task<HttpResponseMessage> TestAsync(ApiClient admin, string method) => admin.PostAsync($"{SettingsPath}/test", new { method });

    private async Task<ApiClient> LdapActiveAsync()
    {
        var (admin, _) = await AdminAsync();
        await SaveLdapAsync(admin);
        using var test = await TestAsync(admin, "ldap");
        using var switched = await SwitchAsync(admin, "ldap");
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        return admin;
    }

    [Fact]
    public async Task A_method_cannot_be_switched_on_before_a_passing_test()
    {
        var (admin, _) = await AdminAsync();
        await SaveLdapAsync(admin);

        using var refused = await SwitchAsync(admin, "ldap");
        factory.LdapDirectory.TestResult = new(false, ErrorCodes.AuthLdapBindFailed);
        using var failedTest = await TestAsync(admin, "ldap");
        using var stillRefused = await SwitchAsync(admin, "ldap");

        Assert.Equal(ErrorCodes.AuthMethodNotTested, await ApiClient.ReadCodeAsync(refused));
        Assert.Equal(ErrorCodes.AuthLdapBindFailed, (await admin.ReadJsonAsync<JsonElement>(failedTest)).GetProperty("errorCode").GetString());
        Assert.Equal(ErrorCodes.AuthMethodNotTested, await ApiClient.ReadCodeAsync(stillRefused));
    }

    [Fact]
    public async Task A_passing_test_allows_the_switch_and_the_login_page_learns_the_method()
    {
        await LdapActiveAsync();

        using var method = await ApiClient.Create(factory).GetAsync("/api/v1/auth/method");

        Assert.Equal("ldap", (await ApiClient.Create(factory).ReadJsonAsync<JsonElement>(method)).GetProperty("method").GetString());
    }

    [Fact]
    public async Task Changing_a_setting_after_the_test_needs_a_new_test()
    {
        var (admin, _) = await AdminAsync();
        await SaveLdapAsync(admin);
        using var test = await TestAsync(admin, "ldap");
        await SaveLdapAsync(admin, host: "dc2.sirket.local");

        using var switched = await SwitchAsync(admin, "ldap");

        Assert.Equal(ErrorCodes.AuthMethodNotTested, await ApiClient.ReadCodeAsync(switched));
    }

    [Fact]
    public async Task Under_a_directory_method_only_admins_sign_in_with_a_local_password()
    {
        await LdapActiveAsync();
        await TestUsers.CreateAsync(factory, UserRole.Employee, "calisan@sirket.com");

        using var employee = await ApiClient.Create(factory).PostAsync("/api/v1/auth/login", new { email = "calisan@sirket.com", password = Password });
        using var admin = await ApiClient.Create(factory).PostAsync("/api/v1/auth/login", new { email = "yonetici@sirket.com", password = Password });

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.OK), (employee.StatusCode, admin.StatusCode));
    }

    [Fact]
    public async Task Directory_users_have_no_local_password_and_cannot_change_one()
    {
        await LdapActiveAsync();
        var client = ApiClient.Create(factory);
        using var login = await client.PostAsync("/api/v1/auth/login/ldap", new { identifier = "ahmet.yilmaz", password = LdapPassword });
        // The antiforgery token is bound to the signed-in user; fetch a new one after login.
        await client.RefreshCsrfAsync();

        using var me = await client.GetAsync("/api/v1/auth/me");
        using var change = await client.PostAsync("/api/v1/auth/change-password", new { currentPassword = "x", newPassword = "YeniŞifre-2026" });

        Assert.False((await client.ReadJsonAsync<JsonElement>(me)).GetProperty("hasLocalPassword").GetBoolean());
        Assert.Equal(ErrorCodes.AuthNoLocalPassword, await ApiClient.ReadCodeAsync(change));
    }

    [Fact]
    public async Task Switching_back_to_local_sends_a_set_password_link_to_every_directory_user()
    {
        var admin = await LdapActiveAsync();
        using (var login = await ApiClient.Create(factory).PostAsync("/api/v1/auth/login/ldap", new { identifier = "ahmet.yilmaz", password = LdapPassword }))
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        using var preview = await admin.GetAsync($"{SettingsPath}/switch-preview?method=local");
        using var switched = await SwitchAsync(admin, "local");

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(1, (await admin.ReadJsonAsync<JsonElement>(preview)).GetProperty("usersWithoutPassword").GetInt32());
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        var directoryUser = await db.Users.SingleAsync(u => u.NormalizedEmail == "ahmet.yilmaz@sirket.com", Ct);
        Assert.True(await db.PasswordTokens.AnyAsync(t => t.UserId == directoryUser.Id && t.Purpose == PasswordTokenPurpose.Reset, Ct));
    }

    [Fact]
    public async Task Forgot_password_sends_nothing_to_a_non_admin_while_a_directory_method_is_active()
    {
        await LdapActiveAsync();
        var employee = await TestUsers.CreateAsync(factory, UserRole.Employee, "calisan@sirket.com");

        using var forgot = await ApiClient.Create(factory).PostAsync("/api/v1/auth/forgot-password", new { email = "calisan@sirket.com" });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(10, TimeSpan.FromMinutes(1), Ct);
        }

        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.PasswordTokens.AnyAsync(t => t.UserId == employee.Id, Ct));
    }

    [Fact]
    public async Task An_admin_creating_a_user_under_a_directory_method_sends_no_email()
    {
        var admin = await LdapActiveAsync();

        using var created = await admin.PostAsync("/api/v1/admin/users", new { firstName = "Ece", lastName = "Nur", email = "ece@sirket.com", role = "employee", departmentId = (Guid?)null, locationId = (Guid?)null });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == "ece@sirket.com", Ct);
        Assert.False(user.HasLocalPassword);
        Assert.False(await db.PasswordTokens.AnyAsync(t => t.UserId == user.Id, Ct));
    }
}
