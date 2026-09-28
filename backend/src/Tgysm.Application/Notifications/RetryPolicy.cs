using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Application-facing view of the retry timing shared with <see cref="OutboxEvent.RecordFailure"/> and
/// <see cref="EmailDelivery.RecordFailure"/> (resolved reading: one source of truth in Domain, since Domain cannot
/// reference Application). A thin wrapper over <see cref="RetryBackoff"/> so callers here do not need to know the
/// concrete Domain type name.
/// </summary>
public static class RetryPolicy
{
    /// <summary>The next attempt time after the <paramref name="attempts"/>-th failure, or <c>null</c> once retries are exhausted (5 attempts).</summary>
    public static DateTimeOffset? NextAttempt(int attempts, DateTimeOffset now) => RetryBackoff.NextAttemptAt(attempts, now);
}
