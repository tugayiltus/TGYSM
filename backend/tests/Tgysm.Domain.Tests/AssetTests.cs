using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tests;

public sealed class AssetTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Employee = Guid.NewGuid();
    private static readonly Guid OtherEmployee = Guid.NewGuid();
    private static readonly Guid Store = Guid.NewGuid();

    private static Asset NewAsset(string? serial = " sn-01 ") =>
        Asset.Create(" dt 0001 ", AssetType.Laptop, "Lenovo", "T14", serial, Store, null, null, null, null, Now).Asset;

    private static (Asset Asset, AssetAssignment Active) Assigned()
    {
        var asset = NewAsset();
        var change = asset.Assign(Employee, Actor, null, null, Now);
        return (asset, change.OpenedAssignment!);
    }

    [Fact]
    public void Create_normalizes_tag_and_serial_and_records_a_movement()
    {
        var (asset, movement) = Asset.Create(" dt 0001 ", AssetType.Laptop, "Lenovo", "T14", "  ", Store, null, null, null, null, Now);

        Assert.Equal("dt 0001", asset.AssetTag);
        Assert.Equal("DT0001", asset.NormalizedAssetTag);
        Assert.Null(asset.SerialNumber);
        Assert.Null(asset.NormalizedSerialNumber);
        Assert.Equal(AssetStatus.InStock, asset.Status);
        Assert.Equal(AssetMovementKind.Created, movement.Kind);
    }

    [Fact]
    public void Serial_is_normalized_without_spaces_in_upper_case() =>
        Assert.Equal("SN-01", NewAsset().NormalizedSerialNumber);

    [Fact]
    public void Assign_from_stock_opens_an_assignment()
    {
        var asset = NewAsset();

        var change = asset.Assign(Employee, Actor, null, "yeni cihaz", Now);

        Assert.Equal(AssetStatus.Assigned, asset.Status);
        Assert.Equal(Employee, change.OpenedAssignment!.UserId);
        Assert.True(change.OpenedAssignment.IsActive);
        Assert.Equal(AssetMovementKind.Assigned, change.Movement.Kind);
        Assert.Equal(AssetStatus.InStock, change.Movement.FromStatus);
    }

    [Fact]
    public void Return_closes_the_assignment_and_moves_the_asset_to_stock()
    {
        var (asset, active) = Assigned();
        var shelf = Guid.NewGuid();

        var change = asset.Return(active, Actor, shelf, Now);

        Assert.Equal(AssetStatus.InStock, asset.Status);
        Assert.Equal(shelf, asset.LocationId);
        Assert.False(active.IsActive);
        Assert.Equal(AssetMovementKind.Returned, change.Movement.Kind);
    }

    [Fact]
    public void Transfer_closes_the_old_assignment_and_opens_a_new_one()
    {
        var (asset, active) = Assigned();

        var change = asset.Transfer(active, OtherEmployee, Actor, null, Now);

        Assert.False(active.IsActive);
        Assert.Equal(OtherEmployee, change.OpenedAssignment!.UserId);
        Assert.Equal(AssetStatus.Assigned, asset.Status);
    }

    [Fact]
    public void Maintenance_that_keeps_responsibility_returns_to_the_same_user()
    {
        var (asset, active) = Assigned();

        var started = asset.StartMaintenance(active, retainResponsibility: true, Actor, Now);
        Assert.True(active.IsActive);
        Assert.True(started.Movement.UserResponsibilityRetained);

        asset.EndMaintenance(active, Actor, Now);
        Assert.Equal(AssetStatus.Assigned, asset.Status);
    }

    [Fact]
    public void Maintenance_without_responsibility_closes_the_assignment_and_ends_in_stock()
    {
        var (asset, active) = Assigned();

        asset.StartMaintenance(active, retainResponsibility: false, Actor, Now);
        Assert.False(active.IsActive);

        asset.EndMaintenance(null, Actor, Now);
        Assert.Equal(AssetStatus.InStock, asset.Status);
    }

    [Fact]
    public void Lost_asset_keeps_history_and_can_only_be_found_or_retired()
    {
        var (asset, active) = Assigned();

        asset.MarkLost(active, Actor, "taksi", Now);

        Assert.Equal(AssetStatus.Lost, asset.Status);
        Assert.False(active.IsActive);
        Assert.Equal(Employee, active.UserId);
        AssertInvalid(() => asset.Assign(Employee, Actor, null, null, Now));

        asset.MarkFound(Actor, Store, Now);
        Assert.Equal(AssetStatus.InStock, asset.Status);
    }

    [Fact]
    public void Retire_closes_an_active_assignment_and_is_final()
    {
        var (asset, active) = Assigned();

        asset.Retire(active, Actor, "ekran kırık", Now);

        Assert.Equal(AssetStatus.Retired, asset.Status);
        Assert.False(active.IsActive);
        AssertInvalid(() => asset.MarkFound(Actor, Store, Now));
        AssertInvalid(() => asset.StartMaintenance(null, false, Actor, Now));
        AssertInvalid(() => asset.Assign(Employee, Actor, null, null, Now));
    }

    [Fact]
    public void Invalid_transitions_are_refused()
    {
        var asset = NewAsset();

        AssertInvalid(() => asset.EndMaintenance(null, Actor, Now));
        AssertInvalid(() => asset.MarkFound(Actor, Store, Now));

        asset.StartMaintenance(null, false, Actor, Now);
        AssertInvalid(() => asset.Assign(Employee, Actor, null, null, Now));
    }

    [Fact]
    public void Assignment_of_another_asset_is_refused()
    {
        var (asset, _) = Assigned();
        var (_, foreign) = Assigned();

        AssertInvalid(() => asset.Return(foreign, Actor, Store, Now));
    }

    private static void AssertInvalid(Action action) =>
        Assert.Equal(ErrorCodes.AssetInvalidTransition, Assert.Throws<DomainException>(action).Code);
}
