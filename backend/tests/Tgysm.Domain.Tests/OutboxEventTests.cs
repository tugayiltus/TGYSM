using Tgysm.Domain.Notifications;

namespace Tgysm.Domain.Tests;

/// <summary>
/// <see cref="OutboxEvent.RecordFailure"/> — the dispatcher's per-event retry/back-off plan (review fix round 1):
/// a bad event backs off instead of blocking the batch, and gives up after <see cref="OutboxEvent.MaxAttempts"/>.
/// </summary>
public sealed class OutboxEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static OutboxEvent CreateEvent() =>
        OutboxEvent.Create(NotificationEventTypes.TicketCreated, Guid.NewGuid(), null, "{}", null, Now);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    public void RecordFailure_backs_off_and_stays_pending_before_the_max_attempt(int attemptNumber, int expectedMinutes)
    {
        var outboxEvent = CreateEvent();
        for (var i = 0; i < attemptNumber - 1; i++)
        {
            outboxEvent.RecordFailure("boom", Now);
        }

        outboxEvent.RecordFailure("boom", Now);

        Assert.Equal(attemptNumber, outboxEvent.Attempts);
        Assert.Equal(OutboxStatus.Pending, outboxEvent.Status);
        Assert.Equal(Now + TimeSpan.FromMinutes(expectedMinutes), outboxEvent.LockedUntil);
    }

    [Fact]
    public void RecordFailure_on_the_fifth_attempt_moves_the_event_to_failed_with_no_further_lock()
    {
        var outboxEvent = CreateEvent();
        for (var i = 0; i < OutboxEvent.MaxAttempts - 1; i++)
        {
            outboxEvent.RecordFailure("boom", Now);
        }

        outboxEvent.RecordFailure("boom", Now);

        Assert.Equal(OutboxEvent.MaxAttempts, outboxEvent.Attempts);
        Assert.Equal(OutboxStatus.Failed, outboxEvent.Status);
        Assert.Null(outboxEvent.LockedUntil);
    }

    [Fact]
    public void RecordFailure_stores_the_error_message()
    {
        var outboxEvent = CreateEvent();

        outboxEvent.RecordFailure("ticket not found", Now);

        Assert.Equal("ticket not found", outboxEvent.LastError);
    }

    [Fact]
    public void RecordFailure_truncates_a_long_error_to_the_email_delivery_last_error_length()
    {
        var outboxEvent = CreateEvent();
        var tooLong = new string('x', EmailDelivery.LastErrorMaxLength + 50);

        outboxEvent.RecordFailure(tooLong, Now);

        Assert.Equal(EmailDelivery.LastErrorMaxLength, outboxEvent.LastError!.Length);
    }
}
