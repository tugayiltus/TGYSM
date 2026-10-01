using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;

namespace Tgysm.Domain.Tests;

/// <summary>
/// <see cref="EmailDelivery"/>'s send/retry state machine (task 6): back-off timing shared with
/// <see cref="OutboxEvent"/> via <see cref="RetryBackoff"/>, the terminal states, and admin retry.
/// </summary>
public sealed class EmailDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Recipient Recipient = new(Guid.NewGuid(), "user@example.com", "Ayşe Yılmaz");

    private static EmailDelivery CreateDelivery(string? body = "Merhaba") =>
        EmailDelivery.Create(Guid.NewGuid(), Recipient, NotificationEventTypes.TicketCreated, "Konu", body, Now);

    [Fact]
    public void Abandon_moves_the_delivery_straight_to_failed_with_the_reason()
    {
        var delivery = CreateDelivery();
        delivery.MarkSending(Now);

        delivery.Abandon("address changed", Now);

        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("address changed", delivery.LastError);
        Assert.Null(delivery.LockedUntil);
        Assert.Equal(0, delivery.Attempts);
    }

    [Fact]
    public void Create_cuts_subject_and_body_to_their_column_limits()
    {
        var delivery = EmailDelivery.Create(Guid.NewGuid(), Recipient, NotificationEventTypes.TicketCreated,
            new string('k', EmailDelivery.SubjectMaxLength + 10), new string('g', EmailDelivery.BodyMaxLength + 10), Now);

        Assert.Equal(EmailDelivery.SubjectMaxLength, delivery.Subject.Length);
        Assert.Equal(EmailDelivery.BodyMaxLength, delivery.Body!.Length);
    }

    [Fact]
    public void Create_never_splits_a_surrogate_pair_at_the_cut()
    {
        const string emoji = "\U0001F600";
        var subject = new string('k', EmailDelivery.SubjectMaxLength - 1) + emoji;

        var delivery = EmailDelivery.Create(Guid.NewGuid(), Recipient, NotificationEventTypes.TicketCreated, subject, null, Now);

        Assert.Equal(EmailDelivery.SubjectMaxLength - 1, delivery.Subject.Length);
        Assert.False(char.IsHighSurrogate(delivery.Subject[^1]));
        Assert.Null(delivery.Body);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    public void RecordFailure_backs_off_and_returns_to_pending_before_the_max_attempt(int attemptNumber, int expectedMinutes)
    {
        var delivery = CreateDelivery();
        for (var i = 0; i < attemptNumber - 1; i++)
        {
            delivery.RecordFailure("boom", Now);
        }

        delivery.RecordFailure("boom", Now);

        Assert.Equal(attemptNumber, delivery.Attempts);
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(Now + TimeSpan.FromMinutes(expectedMinutes), delivery.NextAttemptAt);
        Assert.Null(delivery.LockedUntil);
    }

    [Fact]
    public void RecordFailure_on_the_fifth_attempt_moves_the_delivery_to_failed()
    {
        var delivery = CreateDelivery();
        for (var i = 0; i < RetryBackoff.MaxAttempts - 1; i++)
        {
            delivery.RecordFailure("boom", Now);
        }

        delivery.RecordFailure("boom", Now);

        Assert.Equal(RetryBackoff.MaxAttempts, delivery.Attempts);
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Null(delivery.LockedUntil);
    }

    [Fact]
    public void RecordFailure_truncates_a_long_error()
    {
        var delivery = CreateDelivery();
        var tooLong = new string('x', EmailDelivery.LastErrorMaxLength + 50);

        delivery.RecordFailure(tooLong, Now);

        Assert.Equal(EmailDelivery.LastErrorMaxLength, delivery.LastError!.Length);
    }

    [Fact]
    public void MarkSent_sets_sent_status_timestamp_and_releases_the_lock()
    {
        var delivery = CreateDelivery();
        delivery.MarkSending(Now);

        delivery.MarkSent(Now);

        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal(Now, delivery.SentAt);
        Assert.Null(delivery.LockedUntil);
    }

    [Fact]
    public void ClearBody_replaces_the_body_with_the_fixed_marker()
    {
        var delivery = CreateDelivery(body: null);

        delivery.ClearBody();

        Assert.Equal(EmailDelivery.ClearedBodyMarker, delivery.Body);
    }

    [Fact]
    public void Retry_from_failed_resets_attempts_and_is_due_immediately()
    {
        var delivery = CreateDelivery();
        for (var i = 0; i < RetryBackoff.MaxAttempts; i++)
        {
            delivery.RecordFailure("boom", Now);
        }

        var retryAt = Now.AddDays(1);
        delivery.Retry(retryAt);

        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(0, delivery.Attempts);
        Assert.Equal(retryAt, delivery.NextAttemptAt);
        Assert.Null(delivery.LastError);
        Assert.Null(delivery.LockedUntil);
    }

    [Theory]
    [InlineData(DeliveryStatus.Pending)]
    [InlineData(DeliveryStatus.Sending)]
    [InlineData(DeliveryStatus.Sent)]
    public void Retry_from_anything_other_than_failed_throws(DeliveryStatus status)
    {
        var delivery = CreateDelivery();
        switch (status)
        {
            case DeliveryStatus.Sending:
                delivery.MarkSending(Now);
                break;
            case DeliveryStatus.Sent:
                delivery.MarkSending(Now);
                delivery.MarkSent(Now);
                break;
        }

        var exception = Assert.Throws<DomainException>(() => delivery.Retry(Now));
        Assert.Equal(ErrorCodes.EmailDeliveryNotFailed, exception.Code);
    }
}
