namespace Tgysm.Infrastructure.Persistence;

/// <summary>Failed LDAP sign-ins of one typed name; written only by <see cref="LdapSignInThrottle"/> (ADR-0030).</summary>
public sealed class LdapSignInFailure
{
    public string Key { get; set; } = string.Empty;
    public int FailedCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}
