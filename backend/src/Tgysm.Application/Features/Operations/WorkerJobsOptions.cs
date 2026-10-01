namespace Tgysm.Application.Features.Operations;

/// <summary>
/// The <c>Worker</c> configuration section as the API reads it: only each job's interval, so worker health can
/// compare a heartbeat's age to it. The Worker host binds the same section (its own, richer options type), and
/// both hosts ship the same <c>Worker:Jobs:&lt;name&gt;:IntervalSeconds</c> values.
/// </summary>
public sealed class WorkerJobsOptions
{
    public const string SectionName = "Worker";

    public Dictionary<string, WorkerJobInterval> Jobs { get; init; } = new();
}

public sealed class WorkerJobInterval
{
    public int IntervalSeconds { get; init; }
}
