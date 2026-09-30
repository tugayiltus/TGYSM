namespace Tgysm.Application.Abstractions;

/// <summary>
/// Encrypts an account-link token for an outbox event's <c>protected_secret</c> and reverses it at send time
/// (ADR-0015, ADR-0017), using a protector created for <c>OutboxEvent.ProtectedSecretPurpose</c> on both sides. Kept behind an Application port (like <c>IPasswordService</c>) so Application never takes a
/// direct dependency on the ASP.NET Core Data Protection package; the real <c>IDataProtector</c> lives in the
/// Infrastructure adapter.
/// </summary>
public interface IAccountLinkProtector
{
    string Protect(string token);

    /// <summary>Throws if <paramref name="protectedSecret"/> was not produced by a protector of the same purpose (tampered or wrong key ring).</summary>
    string Unprotect(string protectedSecret);
}
