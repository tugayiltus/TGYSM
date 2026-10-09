using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Assets;

namespace Tgysm.Infrastructure.Persistence.PostMigration;

/// <summary>
/// The AssetCatalog migration built brand and model keys with SQL <c>lower()</c>, whose result depends on the database
/// locale (a C locale lowercases ASCII only). Recomputes every key with <see cref="CatalogName.Normalize"/>; keys that
/// would collide stop the step with both names, so an admin merges them instead of the step merging silently.
/// </summary>
public sealed class RecomputeCatalogNamesStep : IPostMigrationStep
{
    public const string StepId = "2026-10-recompute-catalog-names";

    private const string TemporaryKeyPrefix = "~recompute-";

    public string Id => StepId;

    // Unique per row and never a real key (normalized names have no leading '~' from user input of this length).
    private static string TemporaryKey(Guid id) => TemporaryKeyPrefix + id.ToString("N");

    public async Task RunAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var brands = await db.Brands.AsNoTracking().Select(b => new { b.Id, b.Name, b.NormalizedName }).ToListAsync(cancellationToken);
        var brandClash = brands.GroupBy(b => CatalogName.Normalize(b.Name)).FirstOrDefault(g => g.Count() > 1);
        if (brandClash is not null)
        {
            throw new InvalidOperationException($"Brands would share the key '{brandClash.Key}': {string.Join(", ", brandClash.Select(b => $"'{b.Name}'"))}. Rename one, then run migrate again.");
        }

        var models = await db.AssetModels.AsNoTracking().Select(m => new { m.Id, m.BrandId, m.Type, m.Name, m.NormalizedName }).ToListAsync(cancellationToken);
        var modelClash = models.GroupBy(m => (m.BrandId, m.Type, Key: CatalogName.Normalize(m.Name))).FirstOrDefault(g => g.Count() > 1);
        if (modelClash is not null)
        {
            throw new InvalidOperationException($"Models of one brand and type would share the key '{modelClash.Key.Key}': {string.Join(", ", modelClash.Select(m => $"'{m.Name}'"))}. Rename one, then run migrate again.");
        }

        // Two passes: a new key may equal another row's old (wrong) key, and the unique index checks every statement.
        var changedBrands = brands.Where(b => b.NormalizedName != CatalogName.Normalize(b.Name)).ToList();
        var changedModels = models.Where(m => m.NormalizedName != CatalogName.Normalize(m.Name)).ToList();
        foreach (var brand in changedBrands)
        {
            var placeholder = TemporaryKey(brand.Id);
            await db.Brands.Where(b => b.Id == brand.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.NormalizedName, placeholder), cancellationToken);
        }

        foreach (var model in changedModels)
        {
            var placeholder = TemporaryKey(model.Id);
            await db.AssetModels.Where(m => m.Id == model.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.NormalizedName, placeholder), cancellationToken);
        }

        foreach (var brand in changedBrands)
        {
            var key = CatalogName.Normalize(brand.Name);
            await db.Brands.Where(b => b.Id == brand.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.NormalizedName, key), cancellationToken);
        }

        foreach (var model in changedModels)
        {
            var key = CatalogName.Normalize(model.Name);
            await db.AssetModels.Where(m => m.Id == model.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.NormalizedName, key), cancellationToken);
        }
    }
}
