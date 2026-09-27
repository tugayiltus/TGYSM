using Microsoft.Extensions.Logging;

namespace Tgysm.Worker;

/// <summary>Root of the <c>Worker</c> configuration section: per-job interval, batch size and lease duration.</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public Dictionary<string, JobOptions> Jobs { get; init; } = new();

    /// <summary>
    /// Turns a configured interval (seconds) into a <see cref="TimeSpan"/>; a non-positive value is not usable
    /// (<see cref="PeriodicTimer"/> and <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/> both
    /// reject it), so it falls back to <paramref name="fallback"/> and logs a warning.
    /// </summary>
    public static TimeSpan ResolveInterval(int configured, TimeSpan fallback, ILogger logger)
    {
        if (configured > 0)
        {
            return TimeSpan.FromSeconds(configured);
        }

        logger.LogWarning(
            "Configured interval ({ConfiguredSeconds}s) is not positive; using the default of {DefaultSeconds}s instead.",
            configured, fallback.TotalSeconds);
        return fallback;
    }
}

/// <summary>Per-job settings read from <c>Worker:Jobs:&lt;name&gt;</c>.</summary>
public sealed class JobOptions
{
    public int IntervalSeconds { get; init; }
    public int BatchSize { get; init; }
    public int LeaseSeconds { get; init; }
}
