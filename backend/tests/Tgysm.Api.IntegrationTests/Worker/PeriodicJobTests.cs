using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application;
using Tgysm.Infrastructure;
using Tgysm.Worker;

namespace Tgysm.Api.IntegrationTests.Worker;

/// <summary>
/// <see cref="PeriodicJob"/>'s iteration loop: a failed <c>RunOnceAsync</c> is recorded as a heartbeat and does
/// not stop the next iteration from running and recording success (ADR-0014).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PeriodicJobTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string JobName = "test-job";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await database.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add($"[{logLevel}] {formatter(state, exception)} {exception}");
    }

    private sealed class RecordingJob(
        IServiceScopeFactory scopes,
        IOptionsMonitor<WorkerOptions> options,
        TimeProvider clock,
        ILogger logger,
        Func<Task> onRunOnce)
        : PeriodicJob(scopes, options, clock, logger)
    {
        protected override string Name => JobName;

        protected override Task RunOnceAsync(IServiceProvider scoped, CancellationToken cancellationToken) => onRunOnce();
    }

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new("ConnectionStrings:Default", database.ConnectionString)])
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure();
        services.AddScoped<HeartbeatRecorder>();
        services.Configure<WorkerOptions>(o => o.Jobs[JobName] = new JobOptions { IntervalSeconds = 30 });
        return services.BuildServiceProvider();
    }

    private async Task<(DateTimeOffset LastRunAt, string? LastError)?> FindHeartbeatAsync()
    {
        await using var db = database.CreateDbContext();
        var row = await db.WorkerHeartbeats.AsNoTracking()
            .SingleOrDefaultAsync(h => h.Job == JobName && h.Instance == PeriodicJob.InstanceId, Ct);
        return row is null ? null : (row.LastRunAt, row.LastError);
    }

    /// <summary>
    /// Polls for the heartbeat while nudging <paramref name="clock"/> forward every tick. <see cref="PeriodicJob"/>'s
    /// wait between iterations is <c>Task.Delay(interval, clock, ct)</c>; the fake clock only fires a pending delay
    /// once something advances it past the delay's target, so this keeps pushing it forward until that happens
    /// (or the overall timeout is reached), instead of racing to advance it exactly once at the right moment.
    /// </summary>
    private async Task<(DateTimeOffset LastRunAt, string? LastError)> WaitForHeartbeatAsync(
        FakeTimeProvider clock, Func<string?, bool> matches, CapturingLogger logger)
    {
        var deadline = DateTimeOffset.UtcNow + Timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            var heartbeat = await FindHeartbeatAsync();
            if (heartbeat is { } found && matches(found.LastError))
            {
                return found;
            }

            await Task.Delay(PollInterval, Ct);
        }

        throw new TimeoutException(
            $"No heartbeat for job '{JobName}' reached the expected state in time. Log: {string.Join(" | ", logger.Messages)}");
    }

    [Fact]
    public async Task Exception_in_one_iteration_is_recorded_and_the_next_iteration_runs()
    {
        await using var provider = BuildProvider();
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<WorkerOptions>>();
        var clock = new FakeTimeProvider();
        var logger = new CapturingLogger();

        var attempt = 0;
        Task OnRunOnce()
        {
            var current = Interlocked.Increment(ref attempt);
            return current == 1
                ? throw new InvalidOperationException("boom")
                : Task.CompletedTask;
        }

        var job = new RecordingJob(scopes, optionsMonitor, clock, logger, OnRunOnce);
        await job.StartAsync(Ct);
        try
        {
            var afterFailure = await WaitForHeartbeatAsync(clock, error => error is not null, logger);
            Assert.Contains("boom", afterFailure.LastError, StringComparison.Ordinal);

            var afterSuccess = await WaitForHeartbeatAsync(clock, error => error is null, logger);
            Assert.True(afterSuccess.LastRunAt >= afterFailure.LastRunAt);
        }
        finally
        {
            await job.StopAsync(Ct);
        }

        Assert.True(attempt >= 2, $"Expected at least 2 iterations, got {attempt}.");
    }
}
