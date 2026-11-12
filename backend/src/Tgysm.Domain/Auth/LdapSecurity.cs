namespace Tgysm.Domain.Auth;

/// <summary>Transport security of the LDAP connection; a plain connection cannot be configured (ADR-0030).</summary>
public enum LdapSecurity
{
    Ldaps,
    StartTls,
}
