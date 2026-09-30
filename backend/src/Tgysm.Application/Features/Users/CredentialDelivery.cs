namespace Tgysm.Application.Features.Users;

/// <summary>How a new or reset account gets its password (spec §4, ADR-0017).</summary>
public enum CredentialDelivery
{
    /// <summary>Default: an activation/reset link is emailed; the admin never sees a password.</summary>
    EmailLink,

    /// <summary>Fallback (ADR-0004/ADR-0007 behavior): a temporary password is shown to the admin once.</summary>
    TemporaryPassword,
}
