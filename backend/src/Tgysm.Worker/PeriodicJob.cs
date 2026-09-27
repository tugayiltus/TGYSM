using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tgysm.Worker;

/// <summary>
/// A recurring background job: runs <see cref="RunOnceAsync"/> in its own DI scope on the interval configured for
/// <see cref="Name"/>, catching and recording every iteration's outcome as a heartbeat (ADR-0014). A failed
/// iteration is logged and recorded, never fatal to the loop.
/// </summary>
public abstract class PeriodicJob(
    IServiceScopeFactory scopes,
    IOptionsMonitor<WorkerOptions> options,
    TimeProvider clock,
    ILogger logger) : BackgroundService
{
    /// <summary>Used when <see cref="Name"/> has no entry under <c>Worker:Jobs</c> or its interval is non-positive.</summary>
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(1);

    /// <summary>Identifies this running process for the (job, instance) heartbeat key.</summary>
    public static readonly string InstanceId = $"{Environment.MachineName}:{Environment.ProcessId}";

    protected abstract string Name { get; }

    protected abstract Task RunOnceAsync(IServiceProvider scoped, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var jobOptions = options.CurrentValue.Jobs.GetValueOrDefault(Name) ?? new JobOptions();
            var interval = WorkerOptions.ResolveInterval(jobOptions.IntervalSeconds, DefaultInterval, logger);

            var error = await RunOnceCatchingAsync(stoppingToken);
            await RecordHeartbeatCatchingAsync(error, stoppingToken);

            try
            {
                await Task.Delay(interval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested during the wait; the loop condition below ends it.
            }
        }
    }

    private async Task<string?> RunOnceCatchingAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Job {Job} iteration failed.", Name);
            return exception.Message;
        }
    }

    private async Task RecordHeartbeatCatchingAsync(string? error, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var recorder = scope.ServiceProvider.GetRequiredService<HeartbeatRecorder>();
            await recorder.RecordAsync(Name, InstanceId, error, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Job {Job} could not record its heartbeat.", Name);
        }
    }
}
