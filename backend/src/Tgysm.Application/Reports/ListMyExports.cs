using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Reports;

namespace Tgysm.Application.Reports;

public sealed record ListMyExportsQuery(PageRequest Page);

/// <summary>One background export in "Dışa aktarımlarım".</summary>
public sealed record ExportJobItem(
    Guid Id, ReportName Report, string Metric, ExportJobStatus Status, int? RowCount, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

/// <summary>
/// The signed-in user's own background exports, newest first. A ready job whose lifetime ran out is listed as
/// <see cref="ExportJobStatus.Expired"/> even before cleanup has deleted its file, because it can no longer be downloaded.
/// </summary>
public sealed class ListMyExportsHandler(IAppDbContext db, ICurrentActor actor, TimeProvider clock)
{
    public async Task<PagedResult<ExportJobItem>> HandleAsync(ListMyExportsQuery query, CancellationToken cancellationToken)
    {
        var me = actor.UserId ?? throw new AuthenticationFailedException();
        var now = clock.GetUtcNow();
        return await db.ExportJobs.AsNoTracking()
            .Where(j => j.CreatedBy == me)
            .OrderByDescending(j => j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .Select(j => new ExportJobItem(
                j.Id,
                j.Report,
                j.Metric,
                j.Status == ExportJobStatus.Ready && j.ExpiresAt <= now ? ExportJobStatus.Expired : j.Status,
                j.RowCount,
                j.CreatedAt,
                j.ExpiresAt))
            .ToPagedResultAsync(query.Page, cancellationToken);
    }

    public static ExportJobItem ItemOf(ExportJob job) =>
        new(job.Id, job.Report, job.Metric, job.Status, job.RowCount, job.CreatedAt, job.ExpiresAt);
}
