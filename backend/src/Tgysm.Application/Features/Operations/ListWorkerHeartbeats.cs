using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Features.Operations;

public sealed record WorkerHeartbeatItem(
    string Job,
    string Instance,
    DateTimeOffset LastRunAt,
    string? LastError,
    int IntervalSeconds,
    bool Stale);

/// <summary><see cref="Status"/> is <see cref="WorkerHealth.Ok"/> or <see cref="WorkerHealth.Degraded"/>.</summary>
public sealed record WorkerHeartbeatsResult(string Status, IReadOnlyList<WorkerHeartbeatItem> Jobs);

public static class WorkerHealth
{
    public const string Ok = "ok";
    public const string Degraded = "degraded";

    /// <summary>A job is stale once its last run is older than this many of its own intervals.</summary>
    public const int StaleIntervalMultiplier = 3;

    /// <summary>Mirrors the Worker's fallback for a job with no (or a non-positive) configured interval.</summary>
    public const int DefaultIntervalSeconds = 60;
}

/// <summary>
/// `GET /admin/system/workers` and the health endpoint (ADR-0014): every (job, instance) heartbeat with its
/// configured interval and whether its job is stale (judged by the job's newest heartbeat across instances); any stale job makes the overall status <c>degraded</c>.
/// </summary>
public sealed class ListWorkerHeartbeatsHandler(IAppDbContext db, IOptionsMonitor<WorkerJobsOptions> options, TimeProvider clock)
{
    public async Task<WorkerHeartbeatsResult> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var heartbeats = await db.WorkerHeartbeats.AsNoTracking()
            .OrderBy(h => h.Job).ThenBy(h => h.Instance)
            .ToListAsync(cancellationToken);

        // A restarted worker leaves its old (job, instance) row behind; only the newest run of a job decides.
        var newestRunByJob = heartbeats.GroupBy(h => h.Job).ToDictionary(g => g.Key, g => g.Max(h => h.LastRunAt));
        var jobs = heartbeats.Select(h =>
        {
            var interval = IntervalSecondsOf(h.Job);
            var limit = TimeSpan.FromSeconds((long)interval * WorkerHealth.StaleIntervalMultiplier);
            return new WorkerHeartbeatItem(h.Job, h.Instance, h.LastRunAt, h.LastError, interval, now - newestRunByJob[h.Job] > limit);
        }).ToList();

        return new WorkerHeartbeatsResult(jobs.Any(j => j.Stale) ? WorkerHealth.Degraded : WorkerHealth.Ok, jobs);
    }

    private int IntervalSecondsOf(string job) =>
        options.CurrentValue.Jobs.TryGetValue(job, out var configured) && configured.IntervalSeconds > 0
            ? configured.IntervalSeconds
            : WorkerHealth.DefaultIntervalSeconds;
}
