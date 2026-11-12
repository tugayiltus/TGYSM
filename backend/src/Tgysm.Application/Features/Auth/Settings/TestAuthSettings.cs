using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Auth;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Auth.Settings;

public sealed record TestAuthSettingsCommand(LoginMethod Method);

public sealed record AuthSettingsTestResult(bool Ok, string? ErrorCode);

/// <summary>Checks a provider's settings against the real directory; shared by the test button and by saves of the active method.</summary>
public sealed class LoginProviderTester(IAuthSecretProtector secrets, ILdapDirectory ldap, IEntraConnectionTester entra)
{
    public async Task<AuthSettingsTestResult> TestAsync(AuthSettings settings, LoginMethod method, CancellationToken cancellationToken) => method switch
    {
        LoginMethod.Ldap => await TestLdapAsync(settings, cancellationToken),
        LoginMethod.Entra => await TestEntraAsync(settings, cancellationToken),
        _ => new AuthSettingsTestResult(true, null),
    };

    private async Task<AuthSettingsTestResult> TestLdapAsync(AuthSettings settings, CancellationToken cancellationToken)
    {
        if (settings.LdapHost.Length == 0 || settings.LdapBindPasswordProtected is null)
        {
            return new AuthSettingsTestResult(false, ErrorCodes.AuthLdapBindFailed);
        }

        var test = await ldap.TestAsync(new LdapConnectionSettings(settings.LdapHost, settings.LdapPort, settings.LdapSecurity, settings.LdapBaseDn,
            settings.LdapBindDn, secrets.Unprotect(settings.LdapBindPasswordProtected), settings.LdapUserFilter), cancellationToken);
        return new AuthSettingsTestResult(test.Ok, test.ErrorCode);
    }

    private async Task<AuthSettingsTestResult> TestEntraAsync(AuthSettings settings, CancellationToken cancellationToken)
    {
        if (settings.EntraClientSecretProtected is null)
        {
            return new AuthSettingsTestResult(false, ErrorCodes.AuthEntraSecretInvalid);
        }

        var (ok, errorCode) = await entra.TestAsync(settings.EntraTenantId, settings.EntraClientId, secrets.Unprotect(settings.EntraClientSecretProtected), cancellationToken);
        return new AuthSettingsTestResult(ok, errorCode);
    }
}

/// <summary>
/// "Bağlantıyı test et" (ADR-0030): checks the stored settings of a provider against the real directory. A passing test
/// records a fingerprint of these settings; only then can the method be switched on.
/// </summary>
public sealed class TestAuthSettingsHandler(IAppDbContext db, LoginProviderTester tester, IAuditLog audit)
{
    public async Task<AuthSettingsTestResult> HandleAsync(TestAuthSettingsCommand command, CancellationToken cancellationToken)
    {
        var settings = await db.AuthSettings.SingleAsync(cancellationToken);
        var result = await tester.TestAsync(settings, command.Method, cancellationToken);
        if (result.Ok)
        {
            settings.MarkTested(command.Method);
        }

        audit.Record(AuditActions.AuthSettingsTested, AuditEntityTypes.AuthSettings, settings.Id, newValues: new { command.Method, result.Ok, result.ErrorCode });
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}
