using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Assets;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL sequence: concurrent calls get distinct numbers and a rolled-back save leaves a gap, never a reuse. A number
/// whose tag an existing asset already holds (data from before ADR-0023) is skipped.
/// </summary>
public sealed class AssetTagGenerator(AppDbContext db) : IAssetTagGenerator
{
    public const string SequenceName = "asset_tag_seq";

    public async Task<string> NextAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var values = await db.Database.SqlQueryRaw<long>($"SELECT nextval('{SequenceName}') AS \"Value\"").ToListAsync(cancellationToken);
            var tag = AssetTagFormat.Format(values[0]);
            var normalized = Asset.NormalizeTag(tag);
            if (!await db.Assets.AnyAsync(a => a.NormalizedAssetTag == normalized, cancellationToken))
            {
                return tag;
            }
        }
    }
}
