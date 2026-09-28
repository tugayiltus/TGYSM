namespace Tgysm.Domain.Notifications;

/// <summary>
/// One source of truth for outbox/email retry timing (spec §3.2 step 3; resolved reading: 5 attempts total,
/// back-off 1/5/15/60 minutes after failures 1-4, the 5th failure ends retries instead of scheduling a sixth).
/// Used by both <see cref="OutboxEvent.RecordFailure"/> and <see cref="EmailDelivery.RecordFailure"/> here in
/// Domain, and by Application's <c>RetryPolicy</c> (a thin wrapper — Domain cannot reference Application), so
/// every retry loop in the system applies identical timing.
/// </summary>
public static class RetryBackoff
{
    /// <summary>Retries stop once this many attempts have failed; the <see cref="MaxAttempts"/>-th failure is terminal.</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Delay applied after each failed attempt, indexed by <c>attempts - 1</c> (spec: back-off 1, 5, 15, 60, 360
    /// minutes). The <see cref="MaxAttempts"/>-th failure moves the row straight to its terminal status instead of
    /// scheduling a further retry, so the last entry is never actually applied as a delay; it is kept in the list
    /// because the plan states five values, one per attempt.
    /// </summary>
    private static readonly TimeSpan[] Intervals =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(360),
    ];

    /// <summary>
    /// The next attempt time after the <paramref name="attempts"/>-th failure, or <c>null</c> once
    /// <paramref name="attempts"/> has reached <see cref="MaxAttempts"/> (retries exhausted; the row should move to
    /// its terminal failed status instead).
    /// </summary>
    public static DateTimeOffset? NextAttemptAt(int attempts, DateTimeOffset now) =>
        attempts >= MaxAttempts ? null : now + Intervals[attempts - 1];
}
