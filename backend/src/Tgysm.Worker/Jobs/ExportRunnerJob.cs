using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tgysm.Application.Reports;

namespace Tgysm.Worker.Jobs;

/// <summary>Runs <see cref="RunExportJobsHandler"/> on an interval (ADR-0014, ADR-0020).</summary>
public sealed class ExportRunnerJob(
    IServiceScopeFactory scopes,
    IOptionsMonitor<WorkerOptions> options,
    TimeProvider clock,
    ILogger<ExportRunnerJob> logger)
    : PeriodicJob(scopes, options, clock, logger)
{
    public const string JobName = "ExportRunner";

    /// <summary>Used when <c>Worker:Jobs:ExportRunner</c> leaves a knob unset (zero or missing).</summary>
    private const int DefaultBatchSize = 5;

    /// <summary>One job's lease: long enough to build the largest allowed workbook, so a slow build is never re-claimed mid-run.</summary>
    private const int DefaultLeaseSeconds = 600;

    protected override string Name => JobName;

    protected override async Task RunOnceAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        // Resolved from the scope rather than captured on the primary constructor (CS9107, see EmailSenderJob).
        var currentOptions = scoped.GetRequiredService<IOptionsMonitor<WorkerOptions>>();
        var jobOptions = currentOptions.CurrentValue.Jobs.GetValueOrDefault(Name) ?? new JobOptions();
        var batch = jobOptions.BatchSize > 0 ? jobOptions.BatchSize : DefaultBatchSize;
        var lease = TimeSpan.FromSeconds(jobOptions.LeaseSeconds > 0 ? jobOptions.LeaseSeconds : DefaultLeaseSeconds);

        await scoped.GetRequiredService<RunExportJobsHandler>().HandleAsync(batch, lease, cancellationToken);
    }
}
