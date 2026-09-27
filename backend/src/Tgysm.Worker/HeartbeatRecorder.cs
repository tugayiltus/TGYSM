using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Operations;

namespace Tgysm.Worker;

/// <summary>Upserts the last-run outcome for one (job, instance) pair (ADR-0014).</summary>
public sealed class HeartbeatRecorder(IAppDbContext db, TimeProvider clock)
{
    public async Task RecordAsync(string job, string instance, string? error, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var existing = await db.WorkerHeartbeats
            .SingleOrDefaultAsync(h => h.Job == job && h.Instance == instance, cancellationToken);

        if (existing is null)
        {
            db.WorkerHeartbeats.Add(WorkerHeartbeat.Create(job, instance, now, error));
        }
        else
        {
            existing.Record(now, error);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
