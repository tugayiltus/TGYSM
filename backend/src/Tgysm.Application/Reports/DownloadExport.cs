using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Reports;

public sealed record DownloadExportQuery(Guid Id);

/// <summary>A ready export file, opened for reading; the caller disposes <see cref="Content"/>.</summary>
public sealed record ExportDownload(string FileName, string ContentType, Stream Content);

/// <summary>
/// Downloads a background export (spec §7.3): only its creator, and only while the creator is still active IT staff or admin (any
/// other case is a 404, so a job id reveals nothing). Not built yet → 409 <see cref="ErrorCodes.ReportsExportNotReady"/>; lifetime
/// over (or the file gone) → 410 <see cref="ErrorCodes.ReportsExportExpired"/>; a failed job has no file → 404.
/// </summary>
public sealed class DownloadExportHandler(
    IAppDbContext db, ICurrentActor actor, IFileStore files, IAuditLog audit, TimeProvider clock, ICompanyClock companyClock)
{
    private const string EntityName = "Export";

    public async Task<ExportDownload> HandleAsync(DownloadExportQuery query, CancellationToken cancellationToken)
    {
        var me = actor.UserId ?? throw new AuthenticationFailedException();
        var job = await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == query.Id && j.CreatedBy == me, cancellationToken);
        var creatorMayRunReports = await db.Users.AnyAsync(
            u => u.Id == me && u.IsActive && (u.Role == UserRole.Admin || u.Role == UserRole.ItStaff), cancellationToken);
        if (job is null || !creatorMayRunReports || job.Status == ExportJobStatus.Failed)
        {
            throw new NotFoundException(EntityName);
        }

        if (job.Status is ExportJobStatus.Pending or ExportJobStatus.Running)
        {
            throw new DomainException(ErrorCodes.ReportsExportNotReady, "The export file is not ready yet.");
        }

        var now = clock.GetUtcNow();
        var content = job.IsDownloadable(now) && job.FileKey is { } key ? files.OpenRead(key) : null;
        if (content is null)
        {
            throw new GoneException(ErrorCodes.ReportsExportExpired, "The export file has expired.");
        }

        try
        {
            audit.Record(AuditActions.ReportExportDownloaded, AuditEntityTypes.ExportJob, job.Id);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await content.DisposeAsync();
            throw;
        }

        var fileName = ExportReportHandler.FileNameOf(job.Report, job.Metric, job.FinishedAt ?? job.CreatedAt, companyClock.Zone);
        return new ExportDownload(fileName, ExportReportHandler.XlsxContentType, content);
    }
}
