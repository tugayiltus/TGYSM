namespace Tgysm.Domain.Users;

/// <summary>Why a <see cref="PasswordToken"/> was issued (ADR-0017); stored as its snake_case name.</summary>
public enum PasswordTokenPurpose
{
    /// <summary>First password of an account an admin created with <c>delivery = email_link</c>.</summary>
    Activation,

    /// <summary>Password reset: admin-initiated with <c>delivery = email_link</c>, or self-service forgot-password.</summary>
    Reset,
}
