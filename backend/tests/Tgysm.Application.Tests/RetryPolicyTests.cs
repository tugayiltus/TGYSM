using Tgysm.Application.Notifications;

namespace Tgysm.Application.Tests;

/// <summary>
/// Resolved reading (task-6 brief): 5 attempts total; back-off after failures 1..4 = 1, 5, 15, 60 minutes; the
/// 5th failure ends retries (360 minutes is never actually applied as a delay).
/// </summary>
public sealed class RetryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    public void Next_attempt_backs_off_by_the_configured_minutes(int attempts, int expectedMinutes)
    {
        var next = RetryPolicy.NextAttempt(attempts, Now);

        Assert.Equal(Now + TimeSpan.FromMinutes(expectedMinutes), next);
    }

    [Fact]
    public void Fifth_failure_returns_null_instead_of_a_sixth_back_off()
    {
        var next = RetryPolicy.NextAttempt(5, Now);

        Assert.Null(next);
    }

    [Fact]
    public void Attempts_beyond_five_stay_null()
    {
        var next = RetryPolicy.NextAttempt(6, Now);

        Assert.Null(next);
    }
}
