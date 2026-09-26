using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;

namespace Tgysm.Application.Features.Assets;

public sealed record MyAssetItem(Guid Id, string AssetTag, AssetType Type, string Brand, string Model, string? SerialNumber, DateTimeOffset AssignedAt);

/// <summary>"Zimmetlerim": the signed-in user's active assignments.</summary>
public sealed class ListMyAssetsHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<IReadOnlyList<MyAssetItem>> HandleAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        return await (
            from assignment in db.AssetAssignments.AsNoTracking()
            where assignment.UserId == userId && assignment.ReturnedAt == null
            join asset in db.Assets on assignment.AssetId equals asset.Id
            orderby assignment.AssignedAt descending
            select new MyAssetItem(asset.Id, asset.AssetTag, asset.Type, asset.Brand, asset.Model, asset.SerialNumber, assignment.AssignedAt))
            .ToListAsync(cancellationToken);
    }
}
