using Tgysm.Application.Reports;
using Tgysm.Domain.Reports;

namespace Tgysm.Application.Abstractions;

/// <summary>
/// Everything a report file holds: what was asked (report, metric, bucket, part, filter, scope) and the rows, as cell values by
/// <paramref name="ColumnKeys"/>. Cells are plain values (text, numbers, enums, <see cref="DateTimeOffset"/>, <see cref="DateOnly"/>);
/// the writer decides how to show them, so the same rows can feed any file format. <paramref name="Names"/> maps id text (an id
/// filter or an id bucket) to a display name (<see cref="ReportExportNames"/>); ids missing from it are shown as they are.
/// </summary>
public sealed record ReportExportContent(
    ReportName Report,
    string Metric,
    string? Bucket,
    ReportDrillPart Part,
    ReportFilter Filter,
    string ScopeLabel,
    IReadOnlyList<string> ColumnKeys,
    IAsyncEnumerable<IReadOnlyList<object?>> Rows,
    IReadOnlyDictionary<string, string>? Names = null);

/// <summary>Writes a report file (an .xlsx workbook) to <c>output</c>. The only place with Turkish text for headers and values (spec §7.3).</summary>
public interface IReportFileWriter
{
    Task WriteAsync(ReportExportContent content, Stream output, CancellationToken cancellationToken);
}
