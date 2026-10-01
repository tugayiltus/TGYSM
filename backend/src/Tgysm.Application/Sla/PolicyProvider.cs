using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Sla;

namespace Tgysm.Application.Sla;

/// <summary>Loads the current SLA policy version as a <see cref="SlaPolicySnapshot"/>, the source new SLA instances copy their targets from.</summary>
public sealed class PolicyProvider(IAppDbContext db)
{
    public async Task<SlaPolicySnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var version = await db.SlaPolicyVersions.AsNoTracking()
            .Include(v => v.Targets)
            .Include(v => v.PauseRules)
            .SingleOrDefaultAsync(v => v.IsCurrent, cancellationToken)
            ?? throw new InvalidOperationException("No current SLA policy version exists; the SlaSetup migration seeds one.");
        return new SlaPolicySnapshot(version.Id, version.Targets, version.PauseRules);
    }
}
