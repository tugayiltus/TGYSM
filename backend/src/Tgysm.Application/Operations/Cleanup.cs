using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Reports;

namespace Tgysm.Application.Operations;

/// <summary>Rows removed by one <see cref="CleanupHandler"/> run, plus the export files it deleted.</summary>
public sealed record CleanupResult(int PasswordTokens, int OutboxEvents, int Heartbeats, int ExportFiles);

/// <summary>
/// Retention sweep (ADR-0014, ADR-0015, ADR-0017): deletes password tokens expired, finished outbox events and
/// worker heartbeats last run more than <see cref="Retention"/> ago. Email deliveries of a deleted event go with it
/// (FK cascade). Pending and failed events are kept (failed ones stay visible to admins); an event that still has
/// an unfinished delivery is kept. <c>password_reset_requested</c> events carry a requested email address and never
/// have deliveries, so they are removed once old whatever their status, except while still pending. Ready export files whose
/// lifetime is over (ADR-0020) are deleted from the file store and their jobs marked <see cref="ExportJobStatus.Expired"/>.
/// </summary>
public sealed class CleanupHandler(IAppDbContext db, IFileStore files, TimeProvider clock)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task<CleanupResult> HandleAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - Retention;

        var tokens = await db.PasswordTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var events = await db.OutboxEvents
            .Where(e => (e.Status == OutboxStatus.Dispatched || e.Status == OutboxStatus.Skipped)
                && e.DispatchedAt < cutoff
                && e.EventType != InternalEventTypes.PasswordResetRequested
                && !db.EmailDeliveries.Any(d => d.OutboxId == e.Id
                    && d.Status != DeliveryStatus.Sent && d.Status != DeliveryStatus.Failed))
            .ExecuteDeleteAsync(cancellationToken);

        events += await db.OutboxEvents
            .Where(e => e.EventType == InternalEventTypes.PasswordResetRequested
                && e.Status != OutboxStatus.Pending
                && e.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var heartbeats = await db.WorkerHeartbeats
            .Where(h => h.LastRunAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var exportFiles = await ExpireExportsAsync(cancellationToken);

        return new CleanupResult(tokens, events, heartbeats, exportFiles);
    }

    /// <summary>
    /// Deletes the file before marking the job: a crash in between leaves a ready job without a file (its download answers 410 and the
    /// next run marks it), never a file no job points at. The save is xmin-guarded, so two cleanup runs cannot both mark a job.
    /// </summary>
    private async Task<int> ExpireExportsAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var expired = await db.ExportJobs
            .Where(j => j.Status == ExportJobStatus.Ready && j.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var job in expired)
        {
            if (job.FileKey is { } key)
            {
                files.Delete(key);
            }

            job.Expire(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
