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
    public const string UserActivationLinkSent = "users.activation_link_sent";
    public const string UserPasswordResetLinkSent = "users.password_reset_link_sent";
    public const string UserPasswordSetByLink = "users.password_set_by_link";

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

    public const string CategoryCreated = "categories.created";
    public const string CategoryUpdated = "categories.updated";
    public const string CategoryDeactivated = "categories.deactivated";
    public const string CategoryReactivated = "categories.reactivated";

    public const string BrandCreated = "brands.created";
    public const string BrandRenamed = "brands.renamed";
    public const string BrandDeactivated = "brands.deactivated";
    public const string BrandReactivated = "brands.reactivated";
    public const string AssetModelCreated = "asset_models.created";
    public const string AssetModelRenamed = "asset_models.renamed";
    public const string AssetModelDeactivated = "asset_models.deactivated";
    public const string AssetModelReactivated = "asset_models.reactivated";
    public const string RequestTypeCreated = "request_types.created";
    public const string RequestTypeUpdated = "request_types.updated";
    public const string RequestTypeDeactivated = "request_types.deactivated";
    public const string RequestTypeReactivated = "request_types.reactivated";

    public const string TicketCreated = "tickets.created";
    public const string TicketAssigned = "tickets.assigned";
    public const string TicketTransferred = "tickets.transferred";
    public const string TicketStatusChanged = "tickets.status_changed";
    public const string TicketPriorityChanged = "tickets.priority_changed";
    public const string TicketCompleted = "tickets.completed";
    public const string TicketCancelled = "tickets.cancelled";
    public const string TicketAssetLinked = "tickets.asset_linked";
    public const string TicketAssetUnlinked = "tickets.asset_unlinked";
    public const string RelatedRequestCreated = "tickets.related_request_created";
    public const string ApprovalApproved = "approvals.approved";
    public const string ApprovalRejected = "approvals.rejected";
    public const string ApprovalReassigned = "approvals.reassigned";
    public const string ApprovalVoided = "approvals.voided";

    public const string EmailTemplateUpdated = "email_templates.updated";
    public const string EmailDeliveryRetried = "email_delivery.retried";

    public const string SlaCalendarUpdated = "sla.calendar_updated";
    public const string SlaHolidayAdded = "sla.holiday_added";
    public const string SlaHolidayRemoved = "sla.holiday_removed";
    public const string SlaPolicyVersioned = "sla.policy_versioned";

    public const string KnowledgeCreated = "knowledge.created";
    public const string KnowledgeUpdated = "knowledge.updated";
    public const string KnowledgePublished = "knowledge.published";
    public const string KnowledgeArchived = "knowledge.archived";
    public const string KnowledgeRestored = "knowledge.restored";
    public const string KnowledgeVisibilityChanged = "knowledge.visibility_changed";
    public const string TicketArticleLinked = "tickets.article_linked";
    public const string TicketArticleUnlinked = "tickets.article_unlinked";

    public const string ReportExported = "reports.exported";
    public const string ReportExportRequested = "reports.export_requested";
    public const string ReportExportDownloaded = "reports.export_downloaded";
}
