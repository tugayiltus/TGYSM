using Tgysm.Domain.Common;

namespace Tgysm.Domain.Reports;

/// <summary>
/// A background Excel export (spec §7.3, ADR-0020): what was asked (report, metric, bucket, part, filters as JSON) and who asked.
/// The export runner claims it with a row lease (<see cref="LockedUntil"/>, ADR-0014), builds the file under the creator's scope and
/// marks it <see cref="ExportJobStatus.Ready"/> for <see cref="ReportExportLimits.FileLifetime"/>; cleanup then deletes the file and
/// marks it <see cref="ExportJobStatus.Expired"/>.
/// </summary>
public sealed class ExportJob : Entity
{
    public const int MetricMaxLength = 64;
    public const int BucketMaxLength = 64;
    public const int FileKeyMaxLength = 64;
    public const int LastErrorMaxLength = 500;

    private ExportJob()
    {
    }

    public ReportName Report { get; private init; }

    public string Metric { get; private init; } = string.Empty;

    public string? Bucket { get; private init; }

    public ReportDrillPart Part { get; private init; }

    /// <summary>The report filter as JSON (<c>filters</c> jsonb column).</summary>
    public string Filters { get; private init; } = string.Empty;

    public ExportJobStatus Status { get; private set; } = ExportJobStatus.Pending;

    /// <summary>Rows written to the file; set once the job is ready.</summary>
    public int? RowCount { get; private set; }

    /// <summary>The file store key of a ready file; null before and after.</summary>
    public string? FileKey { get; private set; }

    /// <summary>Why the run failed: an error code or a sanitized exception text (never credentials or row data).</summary>
    public string? LastError { get; private set; }

    public Guid CreatedBy { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Runs started so far (<see cref="Start"/> counts them); see <see cref="ReportExportLimits.MaxAttempts"/>.</summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// Every allowed run has started and none finished: the runs keep crashing the runner, so the job must fail instead of
    /// starting again.
    /// </summary>
    public bool AttemptsExhausted => Attempts >= ReportExportLimits.MaxAttempts;

    /// <summary>Row lease of the runner working on it (ADR-0014); written by the claim, cleared when the run ends.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    public static ExportJob Create(
        ReportName report, string metric, string? bucket, ReportDrillPart part, string filters, Guid createdBy, DateTimeOffset now) =>
        new()
        {
            Report = report,
            Metric = metric,
            Bucket = bucket,
            Part = part,
            Filters = filters,
            CreatedBy = createdBy,
            CreatedAt = now,
        };

    /// <summary>
    /// A runner starts building the file; each start counts as an attempt. A <see cref="ExportJobStatus.Running"/> job whose lease ran
    /// out (a crashed runner) starts again.
    /// </summary>
    public void Start(DateTimeOffset now)
    {
        if (Status is not (ExportJobStatus.Pending or ExportJobStatus.Running))
        {
            throw new InvalidOperationException($"An export job in status {Status} cannot start.");
        }

        Status = ExportJobStatus.Running;
        StartedAt = now;
        Attempts++;
    }

    /// <summary>The file is stored under <paramref name="fileKey"/>; it stays downloadable for <see cref="ReportExportLimits.FileLifetime"/>.</summary>
    public void Complete(string fileKey, int rowCount, DateTimeOffset now)
    {
        if (Status != ExportJobStatus.Running)
        {
            throw new InvalidOperationException($"An export job in status {Status} cannot complete.");
        }

        Status = ExportJobStatus.Ready;
        FileKey = fileKey;
        RowCount = rowCount;
        FinishedAt = now;
        ExpiresAt = now + ReportExportLimits.FileLifetime;
        LockedUntil = null;
    }

    /// <summary>The run failed; <paramref name="sanitizedError"/> must already be safe to store.</summary>
    public void Fail(string sanitizedError, DateTimeOffset now)
    {
        if (Status is not (ExportJobStatus.Pending or ExportJobStatus.Running))
        {
            throw new InvalidOperationException($"An export job in status {Status} cannot fail.");
        }

        Status = ExportJobStatus.Failed;
        LastError = sanitizedError.Length > LastErrorMaxLength ? sanitizedError[..LastErrorMaxLength] : sanitizedError;
        FileKey = null;
        FinishedAt = now;
        LockedUntil = null;
    }

    /// <summary>The lifetime is over and the file was deleted.</summary>
    public void Expire(DateTimeOffset now)
    {
        if (Status != ExportJobStatus.Ready || ExpiresAt > now)
        {
            throw new InvalidOperationException("Only a ready export job past its lifetime can expire.");
        }

        Status = ExportJobStatus.Expired;
        FileKey = null;
    }

    /// <summary>Ready and still inside its lifetime.</summary>
    public bool IsDownloadable(DateTimeOffset now) => Status == ExportJobStatus.Ready && ExpiresAt > now;
}
