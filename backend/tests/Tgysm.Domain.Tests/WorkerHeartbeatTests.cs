using Tgysm.Domain.Operations;

namespace Tgysm.Domain.Tests;

public sealed class WorkerHeartbeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_truncates_an_error_longer_than_the_column_to_the_max_length()
    {
        var longError = new string('x', WorkerHeartbeat.ErrorMaxLength + 500);

        var heartbeat = WorkerHeartbeat.Create("outbox-dispatcher", "host:1", Now, longError);

        Assert.Equal(WorkerHeartbeat.ErrorMaxLength, heartbeat.LastError!.Length);
        Assert.Equal(longError[..WorkerHeartbeat.ErrorMaxLength], heartbeat.LastError);
    }

    [Fact]
    public void Record_also_truncates_an_error_longer_than_the_column_to_the_max_length()
    {
        var heartbeat = WorkerHeartbeat.Create("outbox-dispatcher", "host:1", Now, null);
        var longError = new string('y', WorkerHeartbeat.ErrorMaxLength + 500);

        heartbeat.Record(Now.AddMinutes(1), longError);

        Assert.Equal(WorkerHeartbeat.ErrorMaxLength, heartbeat.LastError!.Length);
    }

    [Fact]
    public void An_error_within_the_column_limit_is_kept_as_is()
    {
        var heartbeat = WorkerHeartbeat.Create("outbox-dispatcher", "host:1", Now, "boom");

        Assert.Equal("boom", heartbeat.LastError);
    }

    [Fact]
    public void A_null_error_stays_null()
    {
        var heartbeat = WorkerHeartbeat.Create("outbox-dispatcher", "host:1", Now, null);

        Assert.Null(heartbeat.LastError);
    }
}
