using Tgysm.Application.Features.Auth.Directory;
using Tgysm.Domain.Auth;

namespace Tgysm.Application.Abstractions;

/// <summary>Connection to the company Active Directory; the bind password is already unprotected (ADR-0030).</summary>
public sealed record LdapConnectionSettings(string Host, int Port, LdapSecurity Security, string BaseDn, string BindDn, string BindPassword, string? UserFilter);

/// <summary>
/// Why a sign-in failed. An unknown user and a wrong password are one kind, so the response never reveals whether a
/// name exists.
/// </summary>
public enum LdapFailure
{
    InvalidCredentials,
    Unavailable,
}

public sealed record LdapResult(DirectoryIdentity? Identity, LdapFailure? Failure)
{
    public static LdapResult Success(DirectoryIdentity identity) => new(identity, null);

    public static LdapResult Fail(LdapFailure failure) => new(null, failure);
}

/// <summary>A connection test; <see cref="ErrorCode"/> is an <c>ErrorCodes</c> value when it failed.</summary>
public sealed record LdapTestResult(bool Ok, string? ErrorCode);

/// <summary>Finds a user by Windows user name or email under the base DN and checks the password with a bind as that user.</summary>
public interface ILdapDirectory
{
    Task<LdapResult> AuthenticateAsync(LdapConnectionSettings settings, string identifier, string password, CancellationToken cancellationToken);

    Task<LdapTestResult> TestAsync(LdapConnectionSettings settings, CancellationToken cancellationToken);
}
