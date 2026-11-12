using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;

namespace Tgysm.Infrastructure.Persistence.PostMigration;

/// <summary>
/// Turns every tag not yet in the <c>ASSET-year-number</c> format (old <c>DMR-</c> tags and hand-typed legacy tags) into
/// that format (ADR-0029): per company calendar year of creation, in creation order, skipping numbers another asset
/// already holds. Sets each year's counter to the last number used, and audits every change with its old and new tag.
/// QR labels keep working: they carry the asset id, not the tag.
/// </summary>
public sealed class RenumberAssetTagsStep : IPostMigrationStep
{
    public const string StepId = "2026-11-renumber-asset-tags";
    private const string CorrelationId = StepId;
    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    public string Id => StepId;

    public async Task RunAsync(AppDbContext db, TimeZoneInfo companyZone, CancellationToken cancellationToken)
    {
        var old = (await db.Assets.AsNoTracking()
                .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                .Select(a => new { a.Id, a.AssetTag, a.CreatedAt })
                .ToListAsync(cancellationToken))
            .Where(a => !AssetTagFormat.IsCurrent(a.AssetTag))
            .ToList();
        if (old.Count == 0)
        {
            return;
        }

        var taken = (await db.Assets.AsNoTracking().Select(a => a.NormalizedAssetTag).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var counters = await db.AssetTagCounters.ToDictionaryAsync(c => c.Year, c => c.LastValue, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var asset in old)
        {
            var year = AssetTagFormat.YearOf(asset.CreatedAt, companyZone);
            var number = counters.GetValueOrDefault(year);
            string tag;
            do
            {
                number += 1;
                tag = AssetTagFormat.Format(year, number);
            }
            while (!taken.Add(Asset.NormalizeTag(tag)));
            counters[year] = number;

            await db.Assets.Where(a => a.Id == asset.Id).ExecuteUpdateAsync(
                s => s.SetProperty(a => a.AssetTag, tag).SetProperty(a => a.NormalizedAssetTag, Asset.NormalizeTag(tag)),
                cancellationToken);
            db.AuditEvents.Add(AuditEvent.Create(
                now,
                actorUserId: null,
                AuditActions.AssetTagRenumbered,
                AuditEntityTypes.Asset,
                asset.Id,
                reason: null,
                JsonSerializer.Serialize(new { asset.AssetTag }, AuditJson),
                JsonSerializer.Serialize(new { AssetTag = tag }, AuditJson),
                CorrelationId));
        }

        foreach (var (year, lastValue) in counters)
        {
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO asset_tag_counters (year, last_value) VALUES ({year}, {lastValue}) ON CONFLICT (year) DO UPDATE SET last_value = GREATEST(asset_tag_counters.last_value, {lastValue})",
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
