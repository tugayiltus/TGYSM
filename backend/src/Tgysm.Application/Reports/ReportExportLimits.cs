namespace Tgysm.Application.Reports;

public static class ReportExportLimits
{
    /// <summary>Largest export built inside the request; bigger ones become a background job.</summary>
    public const int SyncMaxRows = 5000;

    /// <summary>How long a finished export file stays downloadable.</summary>
    public static readonly TimeSpan FileLifetime = TimeSpan.FromHours(24);
}
