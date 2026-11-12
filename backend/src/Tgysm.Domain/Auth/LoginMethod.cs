namespace Tgysm.Domain.Auth;

/// <summary>How people sign in; exactly one is active (ADR-0030).</summary>
public enum LoginMethod
{
    Local,
    Entra,
    Ldap,
}
