namespace Tgysm.Api.Security;

internal static class Policies
{
    /// <summary>Authenticated admin whose password is not pending change.</summary>
    public const string Admin = "admin";

    /// <summary>Authenticated user, even with a pending password change (me, change-password, logout only).</summary>
    public const string PasswordChangePending = "password-change-pending";
}
