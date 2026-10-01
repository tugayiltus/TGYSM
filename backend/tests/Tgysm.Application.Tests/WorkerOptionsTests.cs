using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tgysm.Infrastructure.Email;
using Tgysm.Worker;
using Tgysm.Worker.Jobs;

namespace Tgysm.Application.Tests;

public sealed class WorkerOptionsTests
{
    private static readonly TimeSpan Fallback = TimeSpan.FromMinutes(1);

    private sealed class RecordingLogger : ILogger
    {
        public LogLevel? LoggedLevel { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LoggedLevel = logLevel;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_interval_falls_back_to_the_default_and_logs_a_warning(int configured)
    {
        var logger = new RecordingLogger();

        var resolved = WorkerOptions.ResolveInterval(configured, Fallback, logger);

        Assert.Equal(Fallback, resolved);
        Assert.Equal(LogLevel.Warning, logger.LoggedLevel);
    }

    [Fact]
    public void Positive_interval_is_used_as_seconds_without_logging()
    {
        var logger = new RecordingLogger();

        var resolved = WorkerOptions.ResolveInterval(30, Fallback, logger);

        Assert.Equal(TimeSpan.FromSeconds(30), resolved);
        Assert.Null(logger.LoggedLevel);
    }

    private static WorkerOptions WithEmailSenderLease(int leaseSeconds) => new()
    {
        Jobs = { [EmailSenderJob.JobName] = new JobOptions { LeaseSeconds = leaseSeconds } },
    };

    private static EmailOptions WithSmtpTimeout(int timeoutSeconds) => new() { Smtp = new SmtpOptions { TimeoutSeconds = timeoutSeconds } };

    [Theory]
    [InlineData(60, 30, true)]
    [InlineData(30, 30, false)]
    [InlineData(20, 30, false)]
    [InlineData(0, 0, true)]
    public void Email_sender_lease_must_outlast_the_smtp_timeout(int leaseSeconds, int timeoutSeconds, bool valid)
    {
        Assert.Equal(valid, WorkerOptionsValidation.EmailSenderLeaseOutlastsSmtpTimeout(WithEmailSenderLease(leaseSeconds), WithSmtpTimeout(timeoutSeconds)));
    }

    [Fact]
    public void Unset_lease_and_timeout_resolve_to_the_defaults()
    {
        Assert.Equal(TimeSpan.FromSeconds(EmailSenderJob.DefaultLeaseSeconds), EmailSenderJob.ResolveLease(new WorkerOptions()));
        Assert.Equal(TimeSpan.FromSeconds(SmtpOptions.DefaultTimeoutSeconds), new SmtpOptions().EffectiveTimeout);
    }
}
