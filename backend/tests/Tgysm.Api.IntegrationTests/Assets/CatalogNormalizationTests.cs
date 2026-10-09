using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Assets;
using Tgysm.Infrastructure.Persistence.PostMigration;

namespace Tgysm.Api.IntegrationTests.Assets;

/// <summary>
/// The AssetCatalog migration built name keys with SQL lower(), which depends on the database locale: under a C locale
/// "ÇELİK" became "Çelik", while the domain rule gives "çelik". The post-migration step recomputes every key with
/// <see cref="CatalogName.Normalize"/> once, after migrations.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CatalogNormalizationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SqlLocaleKey = "Çelik";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        await using var db = factory.Database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM maintenance_steps WHERE id = {0}", [RecomputeCatalogNamesStep.StepId], Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Guid> BrandWithKeyAsync(string name, string storedKey)
    {
        var brand = Brand.Create(name, DateTimeOffset.UtcNow);
        await TestData.SaveAsync(factory, db => db.Brands.Add(brand));
        await using var db = factory.Database.CreateDbContext();
        await db.Brands.Where(b => b.Id == brand.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.NormalizedName, storedKey), Ct);
        return brand.Id;
    }

    private async Task<string> KeyOfAsync(Guid brandId)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.Brands.Where(b => b.Id == brandId).Select(b => b.NormalizedName).SingleAsync(Ct);
    }

    private async Task<IReadOnlyList<string>> RunAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await PostMigrationRunner.RunPendingAsync(db, Ct);
    }

    [Fact]
    public async Task A_key_built_by_the_database_locale_is_recomputed_with_the_domain_rule_once()
    {
        var brandId = await BrandWithKeyAsync("ÇELİK", SqlLocaleKey);

        var first = await RunAsync();
        var second = await RunAsync();

        Assert.Equal(CatalogName.Normalize("ÇELİK"), await KeyOfAsync(brandId));
        Assert.Contains(RecomputeCatalogNamesStep.StepId, first);
        Assert.Empty(second);
    }

    [Fact]
    public async Task Keys_that_would_collide_stop_the_step_naming_both_entries_and_change_nothing()
    {
        var upper = await BrandWithKeyAsync("ÇELİK", SqlLocaleKey);
        var lower = await BrandWithKeyAsync("çelik", CatalogName.Normalize("çelik"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);

        Assert.Contains("ÇELİK", error.Message, StringComparison.Ordinal);
        Assert.Contains("çelik", error.Message, StringComparison.Ordinal);
        Assert.Equal(SqlLocaleKey, await KeyOfAsync(upper));
        Assert.Equal(CatalogName.Normalize("çelik"), await KeyOfAsync(lower));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM maintenance_steps WHERE id = {0}", RecomputeCatalogNamesStep.StepId).SingleAsync(Ct));
    }
}
