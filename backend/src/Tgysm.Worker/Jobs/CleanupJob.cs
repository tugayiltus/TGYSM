using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tgysm.Application.Operations;

namespace Tgysm.Worker.Jobs;

/// <summary>Runs <see cref="CleanupHandler"/> on an interval (ADR-0014).</summary>
public sealed class CleanupJob(
    IServiceScopeFactory scopes,
    IOptionsMonitor<WorkerOptions> options,
    TimeProvider clock,
    ILogger<CleanupJob> logger)
    : PeriodicJob(scopes, options, clock, logger)
{
    public const string JobName = "Cleanup";

    protected override string Name => JobName;

    protected override async Task RunOnceAsync(IServiceProvider scoped, CancellationToken cancellationToken)
    {
        var result = await scoped.GetRequiredService<CleanupHandler>().HandleAsync(cancellationToken);
        logger.LogInformation(
            "Cleanup removed {Tokens} password tokens, {Events} outbox events, {Heartbeats} heartbeats.",
            result.PasswordTokens, result.OutboxEvents, result.Heartbeats);
    }
}
