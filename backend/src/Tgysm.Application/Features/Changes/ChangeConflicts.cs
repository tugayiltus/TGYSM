using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Changes;

namespace Tgysm.Application.Features.Changes;

public enum ChangeConflictReason
{
    Asset,
    Service,
}

/// <param name="Number">Null when the other change is outside the viewer's change scope; the window still shows.</param>
/// <param name="Title">Null when the other change is outside the viewer's change scope.</param>
public sealed record ChangeConflict(string? Number, string? Title, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, ChangeConflictReason Reason);

/// <summary>
/// Finds changes whose windows overlap and that touch the same asset or affected service. It is a warning only; it
/// never blocks. Every change in <see cref="ChangeWindow.ConflictStatuses"/> counts as a candidate whatever the viewer
/// may see, so a hidden clash is still reported, just without number and title.
/// </summary>
public sealed class ChangeConflicts(IAppDbContext db, ChangeScope scope)
{
    private sealed record Slot(Guid Id, string Number, string Title, DateTimeOffset Start, DateTimeOffset End, string? Service);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChangeConflict>>> FindAsync(
        IReadOnlyCollection<Guid> changeIds, ChangeViewer viewer, CancellationToken cancellationToken)
    {
        var result = changeIds.Distinct().ToDictionary(id => id, _ => (IReadOnlyList<ChangeConflict>)[]);
        var subjects = await LoadSlotsAsync(db.ChangeRequests.Where(c => changeIds.Contains(c.Id)), cancellationToken);
        if (subjects.Count == 0)
        {
            return result;
        }

        var earliest = subjects.Min(s => s.Start);
        var latest = subjects.Max(s => s.End);
        var candidates = await LoadSlotsAsync(
            db.ChangeRequests.Where(c => c.WindowStart < latest && c.WindowEnd > earliest), cancellationToken);
        var relatedIds = subjects.Select(s => s.Id).Concat(candidates.Select(c => c.Id)).Distinct().ToList();
        var assetLinks = await db.ChangeAssets.AsNoTracking().Where(l => relatedIds.Contains(l.ChangeId))
            .Select(l => new { l.ChangeId, l.AssetId }).ToListAsync(cancellationToken);
        var assetsByChange = assetLinks.GroupBy(l => l.ChangeId).ToDictionary(g => g.Key, g => g.Select(l => l.AssetId).ToHashSet());
        var candidateIds = candidates.Select(c => c.Id).ToList();
        var visible = (await scope.Visible(viewer).AsNoTracking().Where(c => candidateIds.Contains(c.Id)).Select(c => c.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        foreach (var subject in subjects)
        {
            var subjectAssets = assetsByChange.GetValueOrDefault(subject.Id) ?? [];
            var found = new List<ChangeConflict>();
            foreach (var other in candidates.Where(c => c.Id != subject.Id && ChangeWindow.Overlaps(subject.Start, subject.End, c.Start, c.End)))
            {
                var sharesAsset = (assetsByChange.GetValueOrDefault(other.Id) ?? []).Overlaps(subjectAssets);
                if (!sharesAsset && !ChangeWindow.SameService(subject.Service, other.Service))
                {
                    continue;
                }

                var shown = visible.Contains(other.Id);
                found.Add(new ChangeConflict(
                    shown ? other.Number : null, shown ? other.Title : null, other.Start, other.End,
                    sharesAsset ? ChangeConflictReason.Asset : ChangeConflictReason.Service));
            }

            result[subject.Id] = [.. found.OrderBy(c => c.WindowStart).ThenBy(c => c.Number, StringComparer.Ordinal)];
        }

        return result;
    }

    private static async Task<List<Slot>> LoadSlotsAsync(IQueryable<ChangeRequest> source, CancellationToken cancellationToken)
    {
        var rows = await source.AsNoTracking()
            .Where(c => ChangeWindow.ConflictStatuses.Contains(c.Status) && c.WindowStart != null && c.WindowEnd != null)
            .Select(c => new { c.Id, c.Number, c.Title, c.WindowStart, c.WindowEnd, c.AffectedService })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new Slot(r.Id, r.Number, r.Title, r.WindowStart!.Value, r.WindowEnd!.Value, r.AffectedService))];
    }
}
