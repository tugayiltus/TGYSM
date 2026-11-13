using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Changes;

/// <summary>Affected assets of a change: reference check and diffing the link rows.</summary>
internal static class ChangeAssetLinks
{
    private const string AssetIdsField = "AssetIds";
    private const string UnknownAssetMessage = "An affected asset does not exist.";

    /// <summary>Collapses duplicates and refuses ids that are not assets (400 on <c>assetIds</c>).</summary>
    public static async Task<IReadOnlyList<Guid>> ResolveAsync(IAppDbContext db, IReadOnlyList<Guid> assetIds, CancellationToken cancellationToken)
    {
        var distinct = assetIds.Distinct().ToList();
        var known = await db.Assets.Where(a => distinct.Contains(a.Id)).CountAsync(cancellationToken);
        if (known != distinct.Count)
        {
            throw new ValidationException([new ValidationFailure(AssetIdsField, UnknownAssetMessage) { ErrorCode = ErrorCodes.InvalidValue }]);
        }

        return distinct;
    }

    /// <summary>Adds and removes link rows so the change links exactly <paramref name="assetIds"/>; returns what changed.</summary>
    public static async Task<(IReadOnlyList<Guid> Added, IReadOnlyList<Guid> Removed)> SyncAsync(
        IAppDbContext db, ChangeRequest change, IReadOnlyList<Guid> assetIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = await db.ChangeAssets.Where(l => l.ChangeId == change.Id).ToListAsync(cancellationToken);
        var removed = current.Where(l => !assetIds.Contains(l.AssetId)).ToList();
        var added = assetIds.Where(id => current.All(l => l.AssetId != id)).ToList();
        db.ChangeAssets.RemoveRange(removed);
        db.ChangeAssets.AddRange(added.Select(id => ChangeAsset.Create(change.Id, id, now)));
        return (added, [.. removed.Select(l => l.AssetId)]);
    }
}
