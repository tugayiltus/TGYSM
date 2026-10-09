using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Assets;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>PostgreSQL sequence: concurrent calls get distinct numbers and a rolled-back save leaves a gap, never a reuse.</summary>
public sealed class AssetTagGenerator(AppDbContext db) : IAssetTagGenerator
{
    public const string SequenceName = "asset_tag_seq";

    public async Task<string> NextAsync(CancellationToken cancellationToken)
    {
        var values = await db.Database.SqlQueryRaw<long>($"SELECT nextval('{SequenceName}') AS \"Value\"").ToListAsync(cancellationToken);
        return AssetTagFormat.Format(values[0]);
    }
}
