using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Sla;

namespace Tgysm.Application.Sla;

/// <summary>Loads an SLA policy version as a <see cref="SlaPolicySnapshot"/>: the current one for new SLA instances, a stored one for instances already running.</summary>
public sealed class PolicyProvider(IAppDbContext db)
{
    public async Task<SlaPolicySnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var version = await Versions().SingleOrDefaultAsync(v => v.IsCurrent, cancellationToken)
            ?? throw new InvalidOperationException("No current SLA policy version exists; the SlaSetup migration seeds one.");
        return new SlaPolicySnapshot(version.Id, version.Targets, version.PauseRules);
    }

    /// <summary>The version an SLA instance started on; its targets and pause rules stay in force for that instance (ADR-0018).</summary>
    public async Task<SlaPolicySnapshot> GetVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await Versions().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken)
            ?? throw new InvalidOperationException($"SLA policy version {versionId} does not exist.");
        return new SlaPolicySnapshot(version.Id, version.Targets, version.PauseRules);
    }

    private IQueryable<SlaPolicyVersion> Versions() =>
        db.SlaPolicyVersions.AsNoTracking().Include(v => v.Targets).Include(v => v.PauseRules);
}
