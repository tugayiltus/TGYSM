using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Assets;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>
/// Per-year counter upsert (<c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>, as for ticket numbers, ADR-0011): concurrent
/// calls get distinct numbers and a rolled-back save leaves a gap, never a reuse. A number whose tag an existing asset
/// already holds is skipped (ADR-0029).
/// </summary>
public sealed class AssetTagGenerator(AppDbContext db) : IAssetTagGenerator
{
    public const string CounterTable = "asset_tag_counters";

    public async Task<string> NextAsync(int year, CancellationToken cancellationToken)
    {
        while (true)
        {
            var values = await db.Database.SqlQuery<long>(
                    $"""
                    INSERT INTO asset_tag_counters (year, last_value) VALUES ({year}, 1)
                    ON CONFLICT (year) DO UPDATE SET last_value = asset_tag_counters.last_value + 1
                    RETURNING last_value AS "Value"
                    """)
                .ToListAsync(cancellationToken);
            var tag = AssetTagFormat.Format(year, values[0]);
            var normalized = Asset.NormalizeTag(tag);
            if (!await db.Assets.AnyAsync(a => a.NormalizedAssetTag == normalized, cancellationToken))
            {
                return tag;
            }
        }
    }
}
