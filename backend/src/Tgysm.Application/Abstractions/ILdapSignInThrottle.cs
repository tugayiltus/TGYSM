namespace Tgysm.Application.Abstractions;

/// <summary>
/// Counts failed LDAP sign-ins per typed name, known or not, and locks the name after a few failures, before a typical
/// Active Directory lockout threshold, so attempts in TGYSM do not lock the AD account (ADR-0030).
/// </summary>
public interface ILdapSignInThrottle
{
    Task<bool> IsLockedAsync(string key, DateTimeOffset now, CancellationToken cancellationToken);

    Task RegisterFailureAsync(string key, DateTimeOffset now, CancellationToken cancellationToken);

    Task ResetAsync(string key, CancellationToken cancellationToken);
}
