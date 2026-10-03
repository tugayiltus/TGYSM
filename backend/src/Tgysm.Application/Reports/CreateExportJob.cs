using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Reports;

namespace Tgysm.Application.Reports;

public sealed record CreateExportJobCommand(ReportName Report, string Metric, string? Bucket, ReportDrillPart Part, ReportFilter Filter);

/// <summary>
/// Queues a background export (spec §7.3, ADR-0020) for exports too large to stream. Validates the request exactly like the
/// drill-down (unknown metric, bucket, part or filter → 400) and refuses up front an export that already has more than
/// <see cref="ReportExportLimits.JobMaxRows"/> rows (409 <see cref="ErrorCodes.ReportsExportTooLarge"/>). The worker builds the
/// file later under the creator's scope at that time. The audit row names what was asked, never the rows.
/// </summary>
public sealed class CreateExportJobHandler(ReportScope scope, ReportRowSource rows, IAuditLog audit, IAppDbContext db, TimeProvider clock)
{
    public async Task<ExportJobItem> HandleAsync(CreateExportJobCommand command, CancellationToken cancellationToken)
    {
        var viewer = await scope.ViewerAsync(cancellationToken);
        var opened = rows.Open(viewer, command.Report, command.Metric, command.Bucket, command.Part, command.Filter);
        var count = await ReportExportRows.CountAsync(opened, cancellationToken);
        if (count > ReportExportLimits.JobMaxRows)
        {
            throw new DomainException(ErrorCodes.ReportsExportTooLarge, $"Export has {count} rows; the limit is {ReportExportLimits.JobMaxRows}.");
        }

        var job = ExportJob.Create(
            command.Report, command.Metric, command.Bucket, command.Part, command.Filter.ToJson(), viewer.UserId, clock.GetUtcNow());
        db.ExportJobs.Add(job);
        audit.Record(
            AuditActions.ReportExportRequested,
            AuditEntityTypes.ExportJob,
            job.Id,
            newValues: new
            {
                report = ReportNames.ToRoute(command.Report),
                metric = command.Metric,
                bucket = command.Bucket,
                part = ReportFilter.EnumName(command.Part),
                filter = command.Filter.ToQueryString(),
            });
        await db.SaveChangesAsync(cancellationToken);
        return ListMyExportsHandler.ItemOf(job);
    }
}
