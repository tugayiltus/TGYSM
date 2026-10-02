using System.Globalization;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Reports;

public sealed record ExportReportQuery(ReportName Report, string Metric, string? Bucket, ReportDrillPart Part, ReportFilter Filter);

/// <summary>An .xlsx ready to stream: <see cref="WriteAsync"/> reads the rows while it writes, so nothing is built before the response starts.</summary>
public sealed record ReportExportFile(string FileName, string ContentType, Func<Stream, CancellationToken, Task> WriteAsync);

/// <summary>
/// Synchronous Excel export of the rows behind a figure (spec §7.3): the same rows as the drill-down, refused above
/// <see cref="ReportExportLimits.SyncMaxRows"/> (409 <see cref="ErrorCodes.ReportsExportTooLarge"/>). The audit row names what was
/// exported (report, metric, filter), never the rows.
/// </summary>
public sealed class ExportReportHandler(
    ReportScope scope, ReportRowSource rows, IReportFileWriter writer, IAuditLog audit, IAppDbContext db, TimeProvider clock, ICompanyClock companyClock)
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string FileNameFormat = "tgysm-{0}-{1}-{2}.xlsx";
    private const string FileStampFormat = "yyyyMMddHHmm";

    public async Task<ReportExportFile> HandleAsync(ExportReportQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.ViewerAsync(cancellationToken);
        var opened = rows.Open(viewer, query.Report, query.Metric, query.Bucket, query.Part, query.Filter);
        var count = await ReportExportRows.CountAsync(opened, cancellationToken);
        if (count > ReportExportLimits.SyncMaxRows)
        {
            throw new DomainException(ErrorCodes.ReportsExportTooLarge, $"Export has {count} rows; the limit is {ReportExportLimits.SyncMaxRows}.");
        }

        audit.Record(
            AuditActions.ReportExported,
            AuditEntityTypes.Report,
            null,
            newValues: new
            {
                report = ReportNames.ToRoute(query.Report),
                metric = query.Metric,
                bucket = query.Bucket,
                part = ReportFilter.EnumName(query.Part),
                filter = query.Filter.ToQueryString(),
            });
        await db.SaveChangesAsync(cancellationToken);

        var content = ReportExportRows.ContentOf(
            opened, query.Report, query.Metric, query.Bucket, query.Part, query.Filter, GetReportHandler.ScopeLabelOf(viewer, query.Report));
        var stamp = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), companyClock.Zone).ToString(FileStampFormat, CultureInfo.InvariantCulture);
        var fileName = string.Format(CultureInfo.InvariantCulture, FileNameFormat, ReportNames.ToRoute(query.Report), query.Metric, stamp);
        return new ReportExportFile(fileName, XlsxContentType, (output, ct) => writer.WriteAsync(content, output, ct));
    }
}
