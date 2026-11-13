namespace Tgysm.Domain.Changes;

/// <summary>
/// When a task of a change may start or resume (ADR-0032): Hazırlık while scheduled, implementing or verifying;
/// Uygulama only while implementing; Doğrulama only while verifying; Geri dönüş only after the backout started.
/// </summary>
public static class ChangeGate
{
    public static bool CanStart(ChangeStatus status, ChangePhase phase, bool backoutStarted) => phase switch
    {
        ChangePhase.Preparation => status is ChangeStatus.Scheduled or ChangeStatus.Implementing or ChangeStatus.Verifying,
        ChangePhase.Implementation => status == ChangeStatus.Implementing,
        ChangePhase.Verification => status == ChangeStatus.Verifying,
        ChangePhase.Backout => backoutStarted && status is ChangeStatus.Implementing or ChangeStatus.Verifying,
        _ => false,
    };

    /// <summary>
    /// When a task of a change may be completed: as <see cref="CanStart"/>, except that an Uygulama task may still be
    /// finished while verifying, so a reopened or never-started one does not block a successful close.
    /// </summary>
    public static bool CanComplete(ChangeStatus status, ChangePhase phase, bool backoutStarted) =>
        phase == ChangePhase.Implementation
            ? status is ChangeStatus.Implementing or ChangeStatus.Verifying
            : CanStart(status, phase, backoutStarted);
}
