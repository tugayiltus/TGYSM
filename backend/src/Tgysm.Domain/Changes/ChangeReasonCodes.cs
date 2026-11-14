namespace Tgysm.Domain.Changes;

/// <summary>
/// Reasons the system writes itself (ADR-0006: no Turkish text in backend code). They are stored as these codes and
/// the frontend translates them where a reason is shown.
/// </summary>
public static class ChangeReasonCodes
{
    /// <summary>An approved plan was voided because a fingerprint field changed.</summary>
    public const string PlanChanged = "changes.reason.plan_changed";

    /// <summary>A Geri dönüş task was cancelled because the change closed successful.</summary>
    public const string NotNeeded = "changes.reason.not_needed";
}
