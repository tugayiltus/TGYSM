namespace Tgysm.Application.Abstractions;

/// <summary>
/// Reverses the Data Protection encryption on an outbox event's account-link token (ADR-0015, ADR-0017), using a
/// protector created for <c>OutboxEvent.ProtectedSecretPurpose</c> — the same purpose Task 7's issuing side
/// protects with. Kept behind an Application port (like <c>IPasswordService</c>) so Application never takes a
/// direct dependency on the ASP.NET Core Data Protection package; the real <c>IDataProtector</c> lives in the
/// Infrastructure adapter.
/// </summary>
public interface IAccountLinkProtector
{
    /// <summary>Throws if <paramref name="protectedSecret"/> was not produced by a protector of the same purpose (tampered or wrong key ring).</summary>
    string Unprotect(string protectedSecret);
}
