namespace Tgysm.Domain.Common;

/// <summary>
/// Stable error codes returned by the API. Every constant needs a Turkish text in
/// frontend/src/shared/i18n/locales/tr/errors.json (checked by TranslationCoverageTests).
/// </summary>
public static class ErrorCodes
{
    public const string NotFound = "common.not_found";
    public const string Forbidden = "common.forbidden";
    public const string Unexpected = "common.unexpected";
    public const string Duplicate = "common.duplicate";
    public const string ReferenceInactive = "common.reference_inactive";

    public const string ValidationFailed = "validation.failed";
    public const string Required = "validation.required";
    public const string TooLong = "validation.too_long";
    public const string InvalidEmail = "validation.invalid_email";
    public const string InvalidValue = "validation.invalid";

    public const string ConcurrencyConflict = "concurrency.conflict";

    public const string Unauthenticated = "auth.unauthenticated";
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string PasswordChangeRequired = "auth.password_change_required";
    public const string CsrfInvalid = "auth.csrf_invalid";
    public const string CurrentPasswordInvalid = "auth.current_password_invalid";
    public const string PasswordPolicy = "auth.password_policy";
    public const string PasswordSameAsOld = "auth.password_same_as_old";
    public const string RateLimited = "auth.rate_limited";

    public const string SetupAlreadyCompleted = "setup.already_completed";

    public const string EmailTaken = "users.email_taken";
    public const string LastAdmin = "users.last_admin";

    public const string DepartmentNameTaken = "departments.name_taken";
    public const string ApproverInvalid = "departments.approver_invalid";

    public const string LocationNameTaken = "locations.name_taken";
    public const string LocationCycle = "locations.cycle";
    public const string LocationHasActiveChildren = "locations.has_active_children";

    public const string AssetInvalidTransition = "assets.invalid_transition";
    public const string AssetTagTaken = "assets.tag_taken";
    public const string AssetSerialTaken = "assets.serial_taken";
    public const string AssetUserInactive = "assets.user_inactive";

    public const string ImportInvalidHeader = "imports.invalid_header";
    public const string ImportFileTooLarge = "imports.file_too_large";
    public const string ImportTooManyRows = "imports.too_many_rows";
    public const string ImportHasErrors = "imports.has_errors";
    public const string ImportDuplicateInFile = "imports.duplicate_in_file";
    public const string ImportStatusNotAllowed = "imports.status_not_allowed";
    public const string ImportLocationNotFound = "imports.location_not_found";
    public const string ImportLocationAmbiguous = "imports.location_ambiguous";
    public const string ImportDepartmentNotFound = "imports.department_not_found";

    public const string TicketInvalidTransition = "tickets.invalid_transition";
    public const string TicketNoIntakeTeam = "tickets.no_intake_team";
    public const string TicketHoldNoteRequired = "tickets.hold_note_required";
    public const string TicketApprovalPending = "tickets.approval_pending";
    public const string TicketResolutionRequired = "tickets.resolution_required";
    public const string TicketOutcomeRequired = "tickets.outcome_required";
    public const string TicketRelatedRequestRequired = "tickets.related_request_required";
    public const string TicketApprovalRequired = "tickets.approval_required";
    public const string TicketCancelReasonRequired = "tickets.cancel_reason_required";
    public const string TicketAlreadyAssigned = "tickets.already_assigned";
    public const string TicketAssigneeNotInTeam = "tickets.assignee_not_in_team";

    public const string ApprovalNotApprover = "approvals.not_approver";
    public const string ApprovalNotPending = "approvals.not_pending";
    public const string ApprovalReasonRequired = "approvals.reason_required";
    public const string ApprovalApproverInvalid = "approvals.approver_invalid";

    public const string CategoryTooDeep = "categories.too_deep";
    public const string CategoryNameTaken = "categories.name_taken";
    public const string RequestTypeNameTaken = "request_types.name_taken";

    public const string TeamNameTaken = "teams.name_taken";
    public const string MembershipExists = "teams.membership_exists";
    public const string MemberInactive = "teams.member_inactive";
    public const string MemberRoleInvalid = "teams.member_role_invalid";
}
