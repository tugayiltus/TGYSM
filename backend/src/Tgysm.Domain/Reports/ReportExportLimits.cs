namespace Tgysm.Domain.Reports;

public static class ReportExportLimits
{
    /// <summary>Largest export built inside the request; bigger ones become a background job.</summary>
    public const int SyncMaxRows = 5000;

    /// <summary>
    /// Largest background export: ClosedXML builds the whole workbook in memory, so a job above this is refused up front and a job
    /// that grew past it by the time it runs fails with <c>reports.export_too_large</c>.
    /// </summary>
    public const int JobMaxRows = 100_000;

    /// <summary>How long a finished export file stays downloadable.</summary>
    public static readonly TimeSpan FileLifetime = TimeSpan.FromHours(24);
}
