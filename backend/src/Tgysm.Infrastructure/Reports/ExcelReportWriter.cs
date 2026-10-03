using System.Globalization;
using ClosedXML.Excel;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Reports;

namespace Tgysm.Infrastructure.Reports;

/// <summary>
/// Writes the report workbook (spec §7.3): sheet "Kayıtlar" with the rows (frozen header, autofilter, dates in the company time
/// zone) and sheet "Filtreler" with what was asked. Text cells are stored as text, never as formulas, whatever the ticket says.
/// </summary>
internal sealed class ExcelReportWriter(ICompanyClock companyClock, TimeProvider clock) : IReportFileWriter
{
    private const string DateTimeFormat = "dd.MM.yyyy HH:mm";
    private const string DateFormat = "dd.MM.yyyy";
    private const double ColumnWidth = 22;
    private const int TempBufferSize = 81920;
    private const int HeaderRow = 1;
    private const int FirstColumn = 1;
    private const int FieldColumn = 1;
    private const int ValueColumn = 2;

    public async Task WriteAsync(ReportExportContent content, Stream output, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var records = workbook.Worksheets.Add(ExportHeaders.Label(ExportHeaders.Labels.SheetRecords));
        var filters = workbook.Worksheets.Add(ExportHeaders.Label(ExportHeaders.Labels.SheetFilters));

        var rowCount = await WriteRecordsAsync(records, content, cancellationToken);
        WriteFilters(filters, content, rowCount);

        // ClosedXML saves synchronously, which Kestrel refuses on a response body; save to a self-deleting temp file, then copy asynchronously.
        await using var temp = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, TempBufferSize, FileOptions.DeleteOnClose);
        workbook.SaveAs(temp);
        temp.Position = 0;
        await temp.CopyToAsync(output, cancellationToken);
    }

    private async Task<int> WriteRecordsAsync(IXLWorksheet sheet, ReportExportContent content, CancellationToken cancellationToken)
    {
        var columns = content.ColumnKeys;
        for (var c = 0; c < columns.Count; c++)
        {
            sheet.Cell(HeaderRow, FirstColumn + c).Value = ExportHeaders.Column(columns[c]);
        }

        sheet.Row(HeaderRow).Style.Font.Bold = true;
        var row = HeaderRow;
        await foreach (var values in content.Rows.WithCancellation(cancellationToken))
        {
            row++;
            for (var c = 0; c < values.Count; c++)
            {
                WriteCell(sheet.Cell(row, FirstColumn + c), values[c]);
            }
        }

        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Range(HeaderRow, FirstColumn, row, columns.Count).SetAutoFilter();
        sheet.Columns(FirstColumn, columns.Count).Width = ColumnWidth;
        return row - HeaderRow;
    }

    private void WriteCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case DateTimeOffset instant:
                cell.Value = TimeZoneInfo.ConvertTime(instant, companyClock.Zone).DateTime;
                cell.Style.DateFormat.Format = DateTimeFormat;
                break;
            case DateOnly day:
                cell.Value = day.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = DateFormat;
                break;
            case string text:
                cell.SetValue(text);
                break;
            default:
                cell.SetValue(ExportHeaders.EnumCell(value) ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private void WriteFilters(IXLWorksheet sheet, ReportExportContent content, int rowCount)
    {
        sheet.Cell(HeaderRow, FieldColumn).Value = ExportHeaders.Label(ExportHeaders.Labels.Field);
        sheet.Cell(HeaderRow, ValueColumn).Value = ExportHeaders.Label(ExportHeaders.Labels.Value);
        sheet.Row(HeaderRow).Style.Font.Bold = true;

        var row = HeaderRow;
        void Add(string field, string value)
        {
            row++;
            sheet.Cell(row, FieldColumn).SetValue(field);
            sheet.Cell(row, ValueColumn).SetValue(value);
        }

        Add(ExportHeaders.Label(ExportHeaders.Labels.Report), ExportHeaders.Value(content.Report));
        Add(ExportHeaders.Label(ExportHeaders.Labels.Metric), ExportHeaders.Metric(content.Metric));
        if (content.Bucket is not null)
        {
            Add(ExportHeaders.Label(ExportHeaders.Labels.Bucket), NameOf(content, content.Bucket) ?? ExportHeaders.Bucket(content.Bucket));
        }

        Add(ExportHeaders.Label(ExportHeaders.Labels.Part), ExportHeaders.Value(content.Part));
        foreach (var (key, raw) in content.Filter.Entries())
        {
            Add(ExportHeaders.FilterName(key), NameOf(content, raw) ?? ExportHeaders.FilterValue(key, raw));
        }

        Add(ExportHeaders.Label(ExportHeaders.Labels.Scope), ExportHeaders.Scope(content.ScopeLabel));
        Add(ExportHeaders.Label(ExportHeaders.Labels.GeneratedAt), companyClock.Format(clock.GetUtcNow()));
        Add(ExportHeaders.Label(ExportHeaders.Labels.RowCount), rowCount.ToString(CultureInfo.InvariantCulture));
        sheet.Columns(FieldColumn, ValueColumn).Width = ColumnWidth;
    }

    private static string? NameOf(ReportExportContent content, string raw) =>
        content.Names is { } names && names.TryGetValue(raw, out var name) ? name : null;
}
