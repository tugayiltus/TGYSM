using Tgysm.Domain.Common;

namespace Tgysm.Domain.Operations;

/// <summary>
/// Last run outcome for one (job, instance) pair. One row per running job/instance combination; shown on
/// <c>/admin/system</c> so a stale or failing job is visible (ADR-0014).
/// </summary>
public sealed class WorkerHeartbeat : Entity
{
    public const int JobMaxLength = 100;
    public const int InstanceMaxLength = 200;
    public const int ErrorMaxLength = 2000;

    private WorkerHeartbeat()
    {
    }

    public string Job { get; private set; } = string.Empty;
    public string Instance { get; private set; } = string.Empty;
    public DateTimeOffset LastRunAt { get; private set; }
    public string? LastError { get; private set; }

    public static WorkerHeartbeat Create(string job, string instance, DateTimeOffset lastRunAt, string? lastError) =>
        new()
        {
            Job = job,
            Instance = instance,
            LastRunAt = lastRunAt,
            LastError = lastError,
        };

    /// <summary>Records another run's outcome; <paramref name="lastError"/> null on success.</summary>
    public void Record(DateTimeOffset lastRunAt, string? lastError)
    {
        LastRunAt = lastRunAt;
        LastError = lastError;
    }
}
