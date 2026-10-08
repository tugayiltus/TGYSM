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

    /// <summary>
    /// Runs a job may start. A run that takes the whole Worker down never records a failure, so its expired lease makes the job
    /// claimable again; once this many runs have started, the next claim fails the job instead of running it.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Pending plus running background exports one user may have at a time; one more is refused with
    /// <c>reports.export_queue_full</c>, so one user cannot fill the runner's queue.
    /// </summary>
    public const int MaxQueuedPerUser = 3;

    /// <summary>How long a finished export file stays downloadable.</summary>
    public static readonly TimeSpan FileLifetime = TimeSpan.FromHours(24);
}
