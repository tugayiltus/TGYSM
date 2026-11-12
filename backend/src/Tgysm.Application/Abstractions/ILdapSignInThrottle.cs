namespace Tgysm.Application.Abstractions;

/// <summary>
/// Counts failed LDAP sign-ins per typed name, known or not, and locks the name after a few failures, before a typical
/// Active Directory lockout threshold, so attempts in TGYSM do not lock the AD account (ADR-0030).
/// </summary>
public interface ILdapSignInThrottle
{
    /// <summary>
    /// Counts one attempt atomically before the directory is asked, and says whether it may go ahead. The attempt that
    /// reaches the limit starts the lockout, so parallel requests cannot exceed it. A success clears the count.
    /// </summary>
    Task<bool> TryAttemptAsync(string key, DateTimeOffset now, CancellationToken cancellationToken);

    Task ResetAsync(string key, CancellationToken cancellationToken);
}
