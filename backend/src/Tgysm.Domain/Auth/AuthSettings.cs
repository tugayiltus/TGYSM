using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Auth;

/// <summary>
/// The one row that holds the active login method and the settings of each directory provider (ADR-0030). Secrets are
/// stored only in protected form. A provider can be switched on only after its current settings passed a connection test:
/// the test stores a fingerprint of the settings, and any change breaks the match.
/// </summary>
public sealed class AuthSettings
{
    public static readonly Guid SingletonId = new("00000000-0000-7000-8000-00000000a07e");
    public const int HostMaxLength = 253;
    public const int TextMaxLength = 512;
    private const char DomainAt = '@';

    public Guid Id { get; private init; }
    /// <summary>Optimistic concurrency token, mapped to <c>xmin</c> (ADR-0002).</summary>
    public uint Version { get; private set; }
    public LoginMethod Method { get; private set; }
    public string EntraTenantId { get; private set; } = string.Empty;
    public string EntraClientId { get; private set; } = string.Empty;
    public string? EntraClientSecretProtected { get; private set; }
    public List<string> EntraAllowedDomains { get; private set; } = [];
    public string LdapHost { get; private set; } = string.Empty;
    public int LdapPort { get; private set; }
    public LdapSecurity LdapSecurity { get; private set; }
    public string LdapBaseDn { get; private set; } = string.Empty;
    public string LdapBindDn { get; private set; } = string.Empty;
    public string? LdapBindPasswordProtected { get; private set; }
    public string? LdapUserFilter { get; private set; }
    public string? EntraTestedFingerprint { get; private set; }
    public string? LdapTestedFingerprint { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static AuthSettings CreateDefault(DateTimeOffset now) =>
        new() { Id = SingletonId, Method = LoginMethod.Local, LdapSecurity = LdapSecurity.Ldaps, UpdatedAt = now };

    /// <summary>A null secret keeps the stored one (the UI never shows it, so an empty field means "unchanged").</summary>
    public void UpdateEntra(string tenantId, string clientId, string? protectedSecret, IReadOnlyList<string> allowedDomains, DateTimeOffset now)
    {
        EntraTenantId = tenantId.Trim();
        EntraClientId = clientId.Trim();
        EntraClientSecretProtected = protectedSecret ?? EntraClientSecretProtected;
        EntraAllowedDomains = [.. allowedDomains.Select(NormalizeDomain).Where(d => d.Length > 0).Distinct(StringComparer.Ordinal)];
        UpdatedAt = now;
    }

    public void UpdateLdap(string host, int port, LdapSecurity security, string baseDn, string bindDn, string? protectedPassword, string? userFilter, DateTimeOffset now)
    {
        LdapHost = host.Trim();
        LdapPort = port;
        LdapSecurity = security;
        LdapBaseDn = baseDn.Trim();
        LdapBindDn = bindDn.Trim();
        LdapBindPasswordProtected = protectedPassword ?? LdapBindPasswordProtected;
        LdapUserFilter = string.IsNullOrWhiteSpace(userFilter) ? null : userFilter.Trim();
        UpdatedAt = now;
    }

    /// <summary>SHA-256 over every setting of the method, the protected secret included, so any change needs a new test.</summary>
    public string Fingerprint(LoginMethod method)
    {
        var parts = method switch
        {
            LoginMethod.Entra => [EntraTenantId, EntraClientId, EntraClientSecretProtected ?? string.Empty, string.Join(',', EntraAllowedDomains)],
            LoginMethod.Ldap => new[] { LdapHost, LdapPort.ToString(CultureInfo.InvariantCulture), LdapSecurity.ToString(), LdapBaseDn, LdapBindDn, LdapBindPasswordProtected ?? string.Empty, LdapUserFilter ?? string.Empty },
            _ => [],
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }

    public void MarkTested(LoginMethod method)
    {
        if (method == LoginMethod.Entra)
        {
            EntraTestedFingerprint = Fingerprint(method);
        }
        else if (method == LoginMethod.Ldap)
        {
            LdapTestedFingerprint = Fingerprint(method);
        }
    }

    public bool IsTested(LoginMethod method) => method switch
    {
        LoginMethod.Local => true,
        LoginMethod.Entra => EntraTestedFingerprint == Fingerprint(method),
        LoginMethod.Ldap => LdapTestedFingerprint == Fingerprint(method),
        _ => false,
    };

    public void SwitchTo(LoginMethod method, DateTimeOffset now)
    {
        if (!IsTested(method))
        {
            throw new DomainException(ErrorCodes.AuthMethodNotTested, "The settings of this login method have not passed a connection test.");
        }

        Method = method;
        UpdatedAt = now;
    }

    private static string NormalizeDomain(string domain) => domain.Trim().TrimStart(DomainAt).ToLowerInvariant();
}
