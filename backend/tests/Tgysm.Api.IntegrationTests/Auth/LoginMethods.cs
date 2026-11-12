using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Auth;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>Puts the login settings row into a configured, tested and active state for a test (ADR-0030).</summary>
public static class LoginMethods
{
    public const string TenantId = "6f1c2b5e-1a2b-4c3d-8e9f-0a1b2c3d4e5f";
    public const string AllowedDomain = "sirket.com";

    public static async Task EnableLdapAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var secrets = scope.ServiceProvider.GetRequiredService<IAuthSecretProtector>();
        await using var db = factory.Database.CreateDbContext();
        var settings = await db.AuthSettings.SingleAsync(TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        settings.UpdateLdap("dc1.sirket.local", 636, LdapSecurity.Ldaps, "DC=sirket,DC=local", "CN=svc,DC=sirket,DC=local", secrets.Protect("svc-pass"), null, now);
        settings.MarkTested(LoginMethod.Ldap);
        settings.SwitchTo(LoginMethod.Ldap, now);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task EnableEntraAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var secrets = scope.ServiceProvider.GetRequiredService<IAuthSecretProtector>();
        await using var db = factory.Database.CreateDbContext();
        var settings = await db.AuthSettings.SingleAsync(TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        settings.UpdateEntra(TenantId, "istemci-1", secrets.Protect("secret"), [AllowedDomain], now);
        settings.MarkTested(LoginMethod.Entra);
        settings.SwitchTo(LoginMethod.Entra, now);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        // The settings handler drops cached provider options after a change; this direct write must do the same.
        scope.ServiceProvider.GetRequiredService<ILoginProviderCache>().Invalidate();
    }
}
