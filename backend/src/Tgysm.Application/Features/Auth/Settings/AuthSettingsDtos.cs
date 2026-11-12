using Tgysm.Domain.Auth;

namespace Tgysm.Application.Features.Auth.Settings;

/// <summary>Login settings as the admin page shows them: secrets only as "stored or not" (ADR-0030).</summary>
public sealed record AuthSettingsDto(LoginMethod Method, EntraSettingsDto Entra, LdapSettingsDto Ldap, string RedirectUri, uint Version);

public sealed record EntraSettingsDto(string TenantId, string ClientId, bool HasClientSecret, IReadOnlyList<string> AllowedDomains, bool IsTested);

public sealed record LdapSettingsDto(string Host, int Port, LdapSecurity Security, string BaseDn, string BindDn, bool HasBindPassword, string? UserFilter, bool IsTested);

public static class AuthSettingsMapping
{
    /// <summary>Path Microsoft Entra redirects to after sign-in; registered in the Entra app (ADR-0030).</summary>
    public const string OidcCallbackPath = "/api/v1/auth/oidc/callback";

    public static AuthSettingsDto ToDto(AuthSettings settings, string publicBaseUrl) => new(
        settings.Method,
        new EntraSettingsDto(settings.EntraTenantId, settings.EntraClientId, settings.EntraClientSecretProtected is not null, settings.EntraAllowedDomains, settings.IsTested(LoginMethod.Entra)),
        new LdapSettingsDto(settings.LdapHost, settings.LdapPort, settings.LdapSecurity, settings.LdapBaseDn, settings.LdapBindDn, settings.LdapBindPasswordProtected is not null, settings.LdapUserFilter, settings.IsTested(LoginMethod.Ldap)),
        publicBaseUrl.TrimEnd('/') + OidcCallbackPath,
        settings.Version);
}
