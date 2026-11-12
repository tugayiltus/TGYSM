using System.Net;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>Active Directory sign-in through the LDAP port (fake directory; ADR-0030).</summary>
[Collection(ApiCollection.Name)]
public sealed class LdapLoginTests(ApiFactory factory) : IAsyncLifetime
{
    private const string LdapLoginPath = "/api/v1/auth/login/ldap";
    private const string Password = "AdŞifresi-2026";
    private const int LockThreshold = 5;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.LdapDirectory.Reset();
        factory.LdapDirectory.Add("ahmet.yilmaz", "Ahmet.Yilmaz@sirket.com", Password);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<HttpResponseMessage> LoginAsync(ApiClient client, string identifier, string password) =>
        client.PostAsync(LdapLoginPath, new { identifier, password });

    [Fact]
    public async Task The_windows_user_name_and_the_email_sign_in_the_same_person()
    {
        await LoginMethods.EnableLdapAsync(factory);

        using var byName = await LoginAsync(ApiClient.Create(factory), "ahmet.yilmaz", Password);
        using var byEmail = await LoginAsync(ApiClient.Create(factory), "AHMET.YILMAZ@sirket.com", Password);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (byName.StatusCode, byEmail.StatusCode));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedEmail == "ahmet.yilmaz@sirket.com", Ct));
    }

    [Fact]
    public async Task A_session_follows_a_good_sign_in()
    {
        await LoginMethods.EnableLdapAsync(factory);
        var client = ApiClient.Create(factory);

        using var login = await LoginAsync(client, "ahmet.yilmaz", Password);
        using var me = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_name_and_an_unknown_name_answers_the_same()
    {
        await LoginMethods.EnableLdapAsync(factory);
        var client = ApiClient.Create(factory);
        for (var i = 0; i < LockThreshold; i++)
        {
            using var wrong = await LoginAsync(client, "ahmet.yilmaz", "yanlış");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var locked = await LoginAsync(client, "ahmet.yilmaz", Password);
        using var unknown = await LoginAsync(client, "kimse", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await ApiClient.ReadCodeAsync(locked), await ApiClient.ReadCodeAsync(unknown));
    }

    [Fact]
    public async Task An_unreachable_directory_is_reported_as_unavailable()
    {
        await LoginMethods.EnableLdapAsync(factory);
        factory.LdapDirectory.IsDown = true;

        using var login = await LoginAsync(ApiClient.Create(factory), "ahmet.yilmaz", Password);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.Equal(ErrorCodes.AuthDirectoryUnavailable, await ApiClient.ReadCodeAsync(login));
    }

    [Fact]
    public async Task The_ldap_login_is_closed_while_another_method_is_active()
    {
        using var login = await LoginAsync(ApiClient.Create(factory), "ahmet.yilmaz", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
