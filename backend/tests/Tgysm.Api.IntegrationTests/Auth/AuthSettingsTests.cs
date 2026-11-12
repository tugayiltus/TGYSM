using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>Admin login method settings: secrets are write-only and never leave the server (ADR-0030).</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthSettingsTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SettingsPath = "/api/v1/admin/auth-settings";
    private const string ClientSecret = "çok-gizli-istemci-sırrı-123";
    private const string BindPassword = "servis-hesabı-şifresi-456";
    private const string TenantId = "6f1c2b5e-1a2b-4c3d-8e9f-0a1b2c3d4e5f";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<(ApiClient Client, User User)> AdminAsync() => ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "yonetici@example.com");

    private static async Task<uint> VersionAsync(ApiClient client)
    {
        using var get = await client.GetAsync(SettingsPath);
        return (await client.ReadJsonAsync<JsonElement>(get)).GetProperty("version").GetUInt32();
    }

    private static object Entra(uint version, string? secret) =>
        new { tenantId = TenantId, clientId = "istemci-1", clientSecret = secret, allowedDomains = new[] { "sirket.com" }, version };

    private static object Ldap(uint version, string? password, int port = 636) =>
        new { host = "dc1.sirket.local", port, security = "ldaps", baseDn = "DC=sirket,DC=local", bindDn = "CN=svc,DC=sirket,DC=local", bindPassword = password, userFilter = (string?)null, version };

    [Fact]
    public async Task Secrets_are_stored_protected_and_never_returned()
    {
        var (admin, _) = await AdminAsync();

        using var entra = await admin.PutAsync($"{SettingsPath}/entra", Entra(await VersionAsync(admin), ClientSecret));
        using var ldap = await admin.PutAsync($"{SettingsPath}/ldap", Ldap(await VersionAsync(admin), BindPassword));
        using var get = await admin.GetAsync(SettingsPath);
        var body = await get.Content.ReadAsStringAsync(Ct);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (entra.StatusCode, ldap.StatusCode));
        Assert.DoesNotContain(ClientSecret, body, StringComparison.Ordinal);
        Assert.DoesNotContain(BindPassword, body, StringComparison.Ordinal);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.True(json.GetProperty("entra").GetProperty("hasClientSecret").GetBoolean());
        Assert.True(json.GetProperty("ldap").GetProperty("hasBindPassword").GetBoolean());
        Assert.EndsWith("/api/v1/auth/oidc/callback", json.GetProperty("redirectUri").GetString(), StringComparison.Ordinal);

        await using var db = factory.Database.CreateDbContext();
        var row = await db.AuthSettings.AsNoTracking().SingleAsync(Ct);
        Assert.NotEqual(ClientSecret, row.EntraClientSecretProtected);
        Assert.NotEqual(BindPassword, row.LdapBindPasswordProtected);
        var audits = await db.AuditEvents.Where(a => a.Action == AuditActions.AuthSettingsUpdated).Select(a => new { a.OldValues, a.NewValues }).ToListAsync(Ct);
        var auditText = string.Join('\n', audits.Select(a => a.OldValues + a.NewValues));
        Assert.DoesNotContain(ClientSecret, auditText, StringComparison.Ordinal);
        Assert.DoesNotContain(BindPassword, auditText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saving_without_a_secret_keeps_the_stored_one()
    {
        var (admin, _) = await AdminAsync();
        using var first = await admin.PutAsync($"{SettingsPath}/entra", Entra(await VersionAsync(admin), ClientSecret));

        using var second = await admin.PutAsync($"{SettingsPath}/entra", Entra(await VersionAsync(admin), secret: null));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True((await admin.ReadJsonAsync<JsonElement>(second)).GetProperty("entra").GetProperty("hasClientSecret").GetBoolean());
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict()
    {
        var (admin, _) = await AdminAsync();
        var version = await VersionAsync(admin);
        using var first = await admin.PutAsync($"{SettingsPath}/ldap", Ldap(version, BindPassword));

        using var stale = await admin.PutAsync($"{SettingsPath}/ldap", Ldap(version, BindPassword));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Invalid_values_are_field_errors()
    {
        var (admin, _) = await AdminAsync();

        using var ldap = await admin.PutAsync($"{SettingsPath}/ldap", Ldap(await VersionAsync(admin), BindPassword, port: 70000));
        using var entra = await admin.PutAsync($"{SettingsPath}/entra", new { tenantId = "kiracı", clientId = "", clientSecret = "x", allowedDomains = new[] { "sirket" }, version = await VersionAsync(admin) });

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (ldap.StatusCode, entra.StatusCode));
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.ItStaff)]
    public async Task Only_admins_reach_the_settings(UserRole role)
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, role, "kisi@example.com");

        using var get = await client.GetAsync(SettingsPath);

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
    }

    [Fact]
    public async Task Entra_needs_at_least_one_allowed_domain()
    {
        var (admin, _) = await AdminAsync();

        using var response = await admin.PutAsync($"{SettingsPath}/entra", new { tenantId = TenantId, clientId = "istemci-1", clientSecret = "x", allowedDomains = Array.Empty<string>(), version = await VersionAsync(admin) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
