namespace Tgysm.Domain.Audit;

/// <summary>
/// Audit action codes. Every constant needs a Turkish label under "auditActions" in
/// frontend/src/shared/i18n/locales/tr/admin.json (checked by TranslationCoverageTests).
/// </summary>
public static class AuditActions
{
    public const string FirstAdminCreated = "setup.first_admin_created";

    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string LockedOut = "auth.locked_out";
    public const string PasswordChanged = "auth.password_changed";

    public const string UserCreated = "users.created";
    public const string UserUpdated = "users.updated";
    public const string UserRoleChanged = "users.role_changed";
    public const string UserDeactivated = "users.deactivated";
    public const string UserReactivated = "users.reactivated";
    public const string UserPasswordReset = "users.password_reset";

    public const string DepartmentCreated = "departments.created";
    public const string DepartmentUpdated = "departments.updated";
    public const string DepartmentDeactivated = "departments.deactivated";
    public const string DepartmentReactivated = "departments.reactivated";

    public const string LocationCreated = "locations.created";
    public const string LocationUpdated = "locations.updated";
    public const string LocationDeactivated = "locations.deactivated";
    public const string LocationReactivated = "locations.reactivated";

    public const string TeamCreated = "teams.created";
    public const string TeamUpdated = "teams.updated";
    public const string TeamDeactivated = "teams.deactivated";
    public const string TeamReactivated = "teams.reactivated";
    public const string TeamMemberAdded = "teams.member_added";
    public const string TeamMemberRemoved = "teams.member_removed";

    public const string AssetCreated = "assets.created";
    public const string AssetUpdated = "assets.updated";
    public const string AssetAssigned = "assets.assigned";
    public const string AssetReturned = "assets.returned";
    public const string AssetTransferred = "assets.transferred";
    public const string AssetMaintenanceStarted = "assets.maintenance_started";
    public const string AssetMaintenanceEnded = "assets.maintenance_ended";
    public const string AssetLost = "assets.lost";
    public const string AssetFound = "assets.found";
    public const string AssetRetired = "assets.retired";
    public const string AssetsImported = "assets.imported";
}
