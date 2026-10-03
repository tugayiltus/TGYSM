using Tgysm.Domain.Reports;

namespace Tgysm.Application.Abstractions;

/// <summary>
/// Claims export jobs for the export runner (ADR-0014's row-lease pattern applied to <c>export_jobs</c>, as
/// <see cref="IEmailDeliveryClaimer"/> does for <c>email_deliveries</c>): two runners calling this at once get disjoint rows.
/// A <c>running</c> job whose lease ran out belongs to a crashed runner and is claimable again.
/// </summary>
public interface IExportJobClaimer
{
    Task<IReadOnlyList<ExportJob>> ClaimAsync(int batch, TimeSpan lease, CancellationToken cancellationToken);

    /// <summary>
    /// Renews the lease of one claimed job right before it runs: one conditional update that sets a fresh lease of
    /// <paramref name="lease"/> only while the job is still <c>pending</c>/<c>running</c> and still carries
    /// <paramref name="claimedLockedUntil"/> (the lease this caller wrote, used as an ownership token). Returns the new lease value
    /// (the next token), or <c>null</c> when another runner took the job over or finished it, in which case the caller skips it.
    /// </summary>
    Task<DateTimeOffset?> RenewAsync(Guid jobId, DateTimeOffset claimedLockedUntil, TimeSpan lease, CancellationToken cancellationToken);
}
