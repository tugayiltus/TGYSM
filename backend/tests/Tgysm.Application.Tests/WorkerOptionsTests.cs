using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tgysm.Worker;

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
}
