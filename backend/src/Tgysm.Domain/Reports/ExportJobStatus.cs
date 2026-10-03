namespace Tgysm.Domain.Reports;

public enum ExportJobStatus
{
    /// <summary>Requested; waiting for the export runner.</summary>
    Pending,

    /// <summary>Claimed by a runner that is building the file.</summary>
    Running,

    /// <summary>The file is in the file store and downloadable until <see cref="ExportJob.ExpiresAt"/>.</summary>
    Ready,

    /// <summary>The run failed (or the creator may no longer run reports); no file.</summary>
    Failed,

    /// <summary>The lifetime ran out and cleanup deleted the file.</summary>
    Expired,
}
