using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class AssetPersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
    private Location _store = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        _store = Location.Create("Depo", null);
        await using var db = database.CreateDbContext();
        db.Locations.Add(_store);
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Asset NewAsset(string tag, string? serial) =>
        Asset.Create(tag, AssetType.Laptop, "Lenovo", "T14", serial, _store.Id, null, null, null, null, Now).Asset;

    [Fact]
    public async Task Tag_differing_only_in_case_and_spaces_is_a_duplicate()
    {
        await using (var db = database.CreateDbContext())
        {
            db.Assets.Add(NewAsset("DT-0001", null));
            await db.SaveChangesAsync(Ct);
        }

        await using var other = database.CreateDbContext();
        other.Assets.Add(NewAsset(" dt-0001 ", null));

        Assert.Equal(ErrorCodes.Duplicate, (await Assert.ThrowsAsync<DomainException>(() => other.SaveChangesAsync(Ct))).Code);
    }

    [Fact]
    public async Task Serial_numbers_are_unique_but_many_assets_may_have_none()
    {
        await using (var db = database.CreateDbContext())
        {
            db.Assets.AddRange(NewAsset("A1", null), NewAsset("A2", null), NewAsset("A3", "SN-1"));
            await db.SaveChangesAsync(Ct);
        }

        await using var other = database.CreateDbContext();
        other.Assets.Add(NewAsset("A4", "sn-1"));

        Assert.Equal(ErrorCodes.Duplicate, (await Assert.ThrowsAsync<DomainException>(() => other.SaveChangesAsync(Ct))).Code);
    }

    [Fact]
    public async Task An_asset_cannot_have_two_active_assignments()
    {
        var user = User.Create("Ayşe", "Kaya", "ayse@example.com", UserRole.Employee, null, null, "hash", Now);
        var asset = NewAsset("A1", null);
        var change = asset.Assign(user.Id, user.Id, null, null, Now);
        await using (var db = database.CreateDbContext())
        {
            db.Users.Add(user);
            db.Assets.Add(asset);
            db.AssetAssignments.Add(change.OpenedAssignment!);
            db.AssetMovements.Add(change.Movement);
            await db.SaveChangesAsync(Ct);
        }

        // A second handover built from a stale copy of the asset (still "in stock").
        var stale = NewAsset("A-STALE", null);
        var sneaky = stale.Assign(user.Id, user.Id, null, null, Now).OpenedAssignment!;
        await using var other = database.CreateDbContext();
        other.Entry(sneaky).Property(a => a.AssetId).CurrentValue = asset.Id;
        other.AssetAssignments.Add(sneaky);

        Assert.Equal(ErrorCodes.Duplicate, (await Assert.ThrowsAsync<DomainException>(() => other.SaveChangesAsync(Ct))).Code);
        await using var check = database.CreateDbContext();
        Assert.Equal(1, await check.AssetAssignments.CountAsync(a => a.AssetId == asset.Id && a.ReturnedAt == null, Ct));
    }
}
