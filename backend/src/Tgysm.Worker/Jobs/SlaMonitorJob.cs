using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tgysm.Application.Sla;

namespace Tgysm.Worker.Jobs;

/// <summary>Runs <see cref="MonitorSlaHandler"/> on an interval (ADR-0014, ADR-0018).</summary>
public sealed class SlaMonitorJob(
    IServiceScopeFactory scopes,
    IOptionsMonitor<WorkerOptions> options,
    TimeProvider clock,
    ILogger<SlaMonitorJob> logger)
    : PeriodicJob(scopes, options, clock, logger)
{
    public const string JobName = "SlaMonitor";

    /// <summary>Used when <c>Worker:Jobs:SlaMonitor</c> leaves the batch size unset (zero or missing).</summary>
    private const int DefaultBatchSize = 200;

    protected override string Name => JobName;

    protected override async Task RunOnceAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        var currentOptions = scoped.GetRequiredService<IOptionsMonitor<WorkerOptions>>();
        var jobOptions = currentOptions.CurrentValue.Jobs.GetValueOrDefault(Name) ?? new JobOptions();
        var batch = jobOptions.BatchSize > 0 ? jobOptions.BatchSize : DefaultBatchSize;

        await scoped.GetRequiredService<MonitorSlaHandler>().HandleAsync(batch, cancellationToken);
    }
}
