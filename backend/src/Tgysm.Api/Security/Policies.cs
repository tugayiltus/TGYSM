namespace Tgysm.Api.Security;

internal static class Policies
{
    /// <summary>Authenticated admin whose password is not pending change.</summary>
    public const string Admin = "admin";

    /// <summary>IT staff or admin whose password is not pending change (inventory, ticket work).</summary>
    public const string ItStaffOrAdmin = "it-staff-or-admin";

    /// <summary>Authenticated user, even with a pending password change (me, change-password, logout only).</summary>
    public const string PasswordChangePending = "password-change-pending";
}
