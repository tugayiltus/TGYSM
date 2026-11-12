using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Infrastructure.Persistence;
using Tgysm.Infrastructure.Persistence.PostMigration;

namespace Tgysm.Api.IntegrationTests.Assets;

/// <summary>Every tag not yet in the ASSET-year-number format is renumbered once, after migrations (ADR-0029).</summary>
[Collection(ApiCollection.Name)]
public sealed class AssetTagRenumberTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly TimeZoneInfo Company = TimeZoneInfo.FindSystemTimeZoneById(AppOptions.DefaultTimeZone);
    private Guid _storeId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _storeId = (await TestData.LocationAsync(factory, "Depo")).Id;
        await using var db = factory.Database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM maintenance_steps WHERE id = {0}", [RenumberAssetTagsStep.StepId], Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static DateTimeOffset At(int year, int month, int day, int hour = 9) => new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    private async Task<Dictionary<Guid, string>> TagsAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.Assets.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.AssetTag, Ct);
    }

    private async Task RunAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        await PostMigrationRunner.RunPendingAsync(db, Company, Ct);
    }

    [Fact]
    public async Task Every_old_tag_is_renumbered_per_company_year_in_creation_order_and_new_format_tags_stay()
    {
        var june = await TestData.LegacyAssetAsync(factory, "DMR-000002", _storeId, createdAt: At(2025, 6, 1));
        var february = await TestData.LegacyAssetAsync(factory, "DMR-000003", _storeId, createdAt: At(2025, 2, 1));
        // 22:30 UTC on 31 December 2025 is already 2026 in the company calendar.
        var newYearsEve = await TestData.LegacyAssetAsync(factory, "DMR-000001", _storeId, createdAt: new DateTimeOffset(2025, 12, 31, 22, 30, 0, TimeSpan.Zero));
        var imported = await TestData.LegacyAssetAsync(factory, "dmrbs555555", _storeId, createdAt: At(2025, 1, 1));
        var current = await TestData.LegacyAssetAsync(factory, "ASSET-2026-0007", _storeId, createdAt: At(2026, 1, 5));

        await RunAsync();

        var tags = await TagsAsync();
        Assert.Equal("ASSET-2025-0001", tags[imported]);
        Assert.Equal("ASSET-2025-0002", tags[february]);
        Assert.Equal("ASSET-2025-0003", tags[june]);
        Assert.Equal("ASSET-2026-0001", tags[newYearsEve]);
        Assert.Equal("ASSET-2026-0007", tags[current]);
    }

    [Fact]
    public async Task The_counters_continue_after_the_renumbered_tags()
    {
        await TestData.LegacyAssetAsync(factory, "DMR-000001", _storeId, createdAt: At(2025, 3, 1));
        await TestData.LegacyAssetAsync(factory, "DMR-000002", _storeId, createdAt: At(2025, 4, 1));

        await RunAsync();

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal("ASSET-2025-0003", await new AssetTagGenerator(db).NextAsync(2025, Ct));
    }

    [Fact]
    public async Task Each_renumbered_asset_is_audited_with_its_old_and_new_tag()
    {
        var id = await TestData.LegacyAssetAsync(factory, "DMR-000007", _storeId, createdAt: At(2025, 5, 5));

        await RunAsync();

        await using var db = factory.Database.CreateDbContext();
        var audit = await db.AuditEvents.SingleAsync(a => a.Action == AuditActions.AssetTagRenumbered && a.EntityId == id, Ct);
        Assert.Contains("DMR-000007", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains("ASSET-2025-0001", audit.NewValues, StringComparison.Ordinal);
    }
}
