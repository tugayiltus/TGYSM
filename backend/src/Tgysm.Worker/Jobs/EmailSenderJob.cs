using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tgysm.Application.Notifications;

namespace Tgysm.Worker.Jobs;

/// <summary>Runs <see cref="SendDueEmailsHandler"/> on an interval (ADR-0014, ADR-0015).</summary>
public sealed class EmailSenderJob(
    IServiceScopeFactory scopes,
    IOptionsMonitor<WorkerOptions> options,
    TimeProvider clock,
    ILogger<EmailSenderJob> logger)
    : PeriodicJob(scopes, options, clock, logger)
{
    public const string JobName = "EmailSender";

    /// <summary>Used when <c>Worker:Jobs:EmailSender</c> leaves a knob unset (zero or missing).</summary>
    private const int DefaultBatchSize = 50;
    private const int DefaultLeaseSeconds = 60;

    protected override string Name => JobName;

    protected override async Task RunOnceAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        // Resolved from the scope rather than captured on the primary constructor: PeriodicJob's own base
        // constructor already captures the same IOptionsMonitor<WorkerOptions> instance (CS9107).
        var currentOptions = scoped.GetRequiredService<IOptionsMonitor<WorkerOptions>>();
        var jobOptions = currentOptions.CurrentValue.Jobs.GetValueOrDefault(Name) ?? new JobOptions();
        var batch = jobOptions.BatchSize > 0 ? jobOptions.BatchSize : DefaultBatchSize;
        var lease = TimeSpan.FromSeconds(jobOptions.LeaseSeconds > 0 ? jobOptions.LeaseSeconds : DefaultLeaseSeconds);

        var handler = scoped.GetRequiredService<SendDueEmailsHandler>();
        await handler.HandleAsync(batch, lease, cancellationToken);
    }
}
