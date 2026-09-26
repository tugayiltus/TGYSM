using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Assets;

public sealed record AssetMovementItem(
    Guid Id,
    AssetMovementKind Kind,
    AssetStatus? FromStatus,
    AssetStatus ToStatus,
    Guid? UserId,
    string? UserName,
    string? ActorName,
    string? Reason,
    bool? UserResponsibilityRetained,
    DateTimeOffset OccurredAt);

public sealed record AssetHistory(IReadOnlyList<AssetMovementItem> Movements);

public sealed record GetAssetHistoryQuery(Guid AssetId);

public sealed class GetAssetHistoryHandler(IAppDbContext db)
{
    public async Task<AssetHistory> HandleAsync(GetAssetHistoryQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == query.AssetId, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.Asset);
        }

        var movements = await (
            from movement in db.AssetMovements.AsNoTracking()
            where movement.AssetId == query.AssetId
            join user in db.Users on movement.UserId equals (Guid?)user.Id into users
            from user in users.DefaultIfEmpty()
            join actor in db.Users on movement.ActorUserId equals actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby movement.OccurredAt descending, movement.Id descending
            select new AssetMovementItem(
                movement.Id,
                movement.Kind,
                movement.FromStatus,
                movement.ToStatus,
                movement.UserId,
                user == null ? null : user.FirstName + " " + user.LastName,
                actor == null ? null : actor.FirstName + " " + actor.LastName,
                movement.Reason,
                movement.UserResponsibilityRetained,
                movement.OccurredAt))
            .ToListAsync(cancellationToken);

        return new AssetHistory(movements);
    }
}
