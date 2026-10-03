using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Reports;

namespace Tgysm.Infrastructure.Persistence;

/// <summary><see cref="IExportJobClaimer"/> over <see cref="RowLease"/> (ADR-0014), the pattern <see cref="EmailDeliveryClaimer"/> uses.</summary>
internal sealed class ExportJobClaimer(IAppDbContext db) : IExportJobClaimer
{
    // A 'running' job whose lease has expired belongs to a crashed runner: it becomes claimable again. A live lease
    // keeps the row out of every other claim; without that check a second runner re-claims a job already in progress.
    private const string DuePredicate =
        "status IN ('pending', 'running') AND (locked_until IS NULL OR locked_until < now())";

    // Lease time comes from the database clock, exactly like RowLease, so both claims compare against one clock.
    private const string RenewSql = @"
        UPDATE export_jobs SET locked_until = now() + ({0} * interval '1 second')
        WHERE id = {1} AND locked_until = {2} AND status IN ('pending', 'running')
        RETURNING export_jobs.*, export_jobs.xmin";

    public async Task<IReadOnlyList<ExportJob>> ClaimAsync(int batch, TimeSpan lease, CancellationToken cancellationToken) =>
        await RowLease.ClaimAsync(db.ExportJobs, DuePredicate, batch, lease, cancellationToken);

    public async Task<DateTimeOffset?> RenewAsync(Guid jobId, DateTimeOffset claimedLockedUntil, TimeSpan lease, CancellationToken cancellationToken)
    {
        var leaseSeconds = (int)lease.TotalSeconds;
        var renewed = await db.ExportJobs
            .FromSqlRaw(RenewSql, leaseSeconds, jobId, claimedLockedUntil)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return renewed.Count == 1 ? renewed[0].LockedUntil : null;
    }
}
