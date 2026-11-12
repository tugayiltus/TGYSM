using Tgysm.Application.Features.Auth.Directory;
using Tgysm.Domain.Auth;

namespace Tgysm.Application.Abstractions;

/// <summary>Connection to the company Active Directory; the bind password is already unprotected (ADR-0030).</summary>
public sealed record LdapConnectionSettings(string Host, int Port, LdapSecurity Security, string BaseDn, string BindDn, string BindPassword, string? UserFilter);

/// <summary>The outcome of a lookup or a password check.</summary>
public enum LdapStatus
{
    Ok,
    NotFound,
    InvalidCredentials,
    Unavailable,
}

/// <summary>A user found under the base DN: the entry to bind as and the identity it maps to.</summary>
public sealed record LdapEntryMatch(LdapStatus Status, string? DistinguishedName, DirectoryIdentity? Identity);

/// <summary>A connection test; <see cref="ErrorCode"/> is an <c>ErrorCodes</c> value when it failed.</summary>
public sealed record LdapTestResult(bool Ok, string? ErrorCode);

/// <summary>
/// Active Directory in two steps (ADR-0030): find the user by Windows user name or email with the service account, then
/// check the password by binding as that entry. Splitting them lets the caller throttle on the found account, not only on
/// the typed name.
/// </summary>
public interface ILdapDirectory
{
    Task<LdapEntryMatch> FindAsync(LdapConnectionSettings settings, string identifier, CancellationToken cancellationToken);

    Task<LdapStatus> CheckPasswordAsync(LdapConnectionSettings settings, string distinguishedName, string password, CancellationToken cancellationToken);

    Task<LdapTestResult> TestAsync(LdapConnectionSettings settings, CancellationToken cancellationToken);
}
