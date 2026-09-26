using Tgysm.Domain.Common;

namespace Tgysm.Domain.Assets;

/// <summary>IT asset with an explicit lifecycle state machine (ADR-0010).</summary>
public sealed class Asset : Entity
{
    public const int TagMaxLength = 50;
    public const int BrandMaxLength = 100;
    public const int ModelMaxLength = 100;
    public const int SerialMaxLength = 100;
    public const int NotesMaxLength = 1000;

    private Asset()
    {
    }

    public string AssetTag { get; private set; } = string.Empty;
    public string NormalizedAssetTag { get; private set; } = string.Empty;
    public AssetType Type { get; private set; }
    public string Brand { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string? SerialNumber { get; private set; }
    public string? NormalizedSerialNumber { get; private set; }
    public AssetStatus Status { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public DateOnly? WarrantyEndsOn { get; private set; }
    public DateOnly? PurchasedOn { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Asset tags compare without whitespace and case ("dt 0001" = "DT0001").</summary>
    public static string NormalizeTag(string tag) => RemoveWhitespace(tag).ToUpperInvariant();

    /// <summary>Unknown serial numbers are null, never a placeholder.</summary>
    public static string? NormalizeSerial(string? serial) =>
        string.IsNullOrWhiteSpace(serial) ? null : RemoveWhitespace(serial).ToUpperInvariant();

    public static (Asset Asset, AssetMovement Movement) Create(
        string assetTag,
        AssetType type,
        string brand,
        string model,
        string? serialNumber,
        Guid locationId,
        Guid? departmentId,
        DateOnly? warrantyEndsOn,
        DateOnly? purchasedOn,
        string? notes,
        DateTimeOffset now,
        AssetStatus initialStatus = AssetStatus.InStock,
        Guid actorUserId = default)
    {
        if (initialStatus is AssetStatus.Assigned or AssetStatus.Lost)
        {
            throw InvalidTransition(initialStatus, "create");
        }

        var asset = new Asset { Status = initialStatus, CreatedAt = now };
        asset.UpdateCard(assetTag, type, brand, model, serialNumber, departmentId, warrantyEndsOn, purchasedOn, notes, now);
        asset.LocationId = locationId;
        var movement = AssetMovement.Record(asset.Id, AssetMovementKind.Created, null, initialStatus, null, actorUserId, null, null, now);
        return (asset, movement);
    }

    private void UpdateCard(
        string assetTag,
        AssetType type,
        string brand,
        string model,
        string? serialNumber,
        Guid? departmentId,
        DateOnly? warrantyEndsOn,
        DateOnly? purchasedOn,
        string? notes,
        DateTimeOffset now)
    {
        AssetTag = assetTag.Trim();
        NormalizedAssetTag = NormalizeTag(assetTag);
        Type = type;
        Brand = brand.Trim();
        Model = model.Trim();
        SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();
        NormalizedSerialNumber = NormalizeSerial(serialNumber);
        DepartmentId = departmentId;
        WarrantyEndsOn = warrantyEndsOn;
        PurchasedOn = purchasedOn;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = now;
    }

    /// <summary>Card edit (descriptive fields and location); status never changes here.</summary>
    public AssetMovement Update(
        string assetTag,
        AssetType type,
        string brand,
        string model,
        string? serialNumber,
        Guid locationId,
        Guid? departmentId,
        DateOnly? warrantyEndsOn,
        DateOnly? purchasedOn,
        string? notes,
        Guid actorUserId,
        DateTimeOffset now)
    {
        UpdateCard(assetTag, type, brand, model, serialNumber, departmentId, warrantyEndsOn, purchasedOn, notes, now);
        LocationId = locationId;
        return AssetMovement.Record(Id, AssetMovementKind.Updated, Status, Status, null, actorUserId, null, null, now);
    }

    public AssetChange Assign(Guid userId, Guid actorUserId, Guid? requestTicketId, string? note, DateTimeOffset now)
    {
        Require(AssetStatus.InStock, nameof(Assign));
        var assignment = AssetAssignment.Open(Id, userId, actorUserId, requestTicketId, note, now);
        return new AssetChange(ChangeStatus(AssetMovementKind.Assigned, AssetStatus.Assigned, userId, actorUserId, note, null, now), assignment);
    }

    public AssetChange Return(AssetAssignment active, Guid actorUserId, Guid locationId, DateTimeOffset now)
    {
        Require(AssetStatus.Assigned, nameof(Return));
        CloseOwn(active, actorUserId, now);
        LocationId = locationId;
        return new AssetChange(ChangeStatus(AssetMovementKind.Returned, AssetStatus.InStock, active.UserId, actorUserId, null, null, now));
    }

    public AssetChange Transfer(AssetAssignment active, Guid newUserId, Guid actorUserId, string? note, DateTimeOffset now)
    {
        Require(AssetStatus.Assigned, nameof(Transfer));
        CloseOwn(active, actorUserId, now);
        var assignment = AssetAssignment.Open(Id, newUserId, actorUserId, null, note, now);
        return new AssetChange(ChangeStatus(AssetMovementKind.Transferred, AssetStatus.Assigned, newUserId, actorUserId, note, null, now), assignment);
    }

    /// <param name="active">The active assignment when the asset is assigned; null from stock.</param>
    public AssetChange StartMaintenance(AssetAssignment? active, bool retainResponsibility, Guid actorUserId, DateTimeOffset now)
    {
        Require([AssetStatus.InStock, AssetStatus.Assigned], nameof(StartMaintenance));
        var retained = Status == AssetStatus.Assigned && retainResponsibility;
        if (Status == AssetStatus.Assigned && !retainResponsibility)
        {
            CloseOwn(active, actorUserId, now);
        }

        return new AssetChange(ChangeStatus(AssetMovementKind.MaintenanceStarted, AssetStatus.Maintenance, active?.UserId, actorUserId, null, retained, now));
    }

    /// <param name="active">The assignment kept during maintenance, or null.</param>
    public AssetChange EndMaintenance(AssetAssignment? active, Guid actorUserId, DateTimeOffset now)
    {
        Require(AssetStatus.Maintenance, nameof(EndMaintenance));
        if (active is not null)
        {
            EnsureOwn(active);
        }

        var target = active is { IsActive: true } ? AssetStatus.Assigned : AssetStatus.InStock;
        return new AssetChange(ChangeStatus(AssetMovementKind.MaintenanceEnded, target, active?.UserId, actorUserId, null, null, now));
    }

    public AssetChange MarkLost(AssetAssignment? active, Guid actorUserId, string reason, DateTimeOffset now)
    {
        Require([AssetStatus.InStock, AssetStatus.Assigned, AssetStatus.Maintenance], nameof(MarkLost));
        CloseIfActive(active, actorUserId, now);
        return new AssetChange(ChangeStatus(AssetMovementKind.Lost, AssetStatus.Lost, active?.UserId, actorUserId, reason, null, now));
    }

    public AssetChange MarkFound(Guid actorUserId, Guid locationId, DateTimeOffset now)
    {
        Require(AssetStatus.Lost, nameof(MarkFound));
        LocationId = locationId;
        return new AssetChange(ChangeStatus(AssetMovementKind.Found, AssetStatus.InStock, null, actorUserId, null, null, now));
    }

    public AssetChange Retire(AssetAssignment? active, Guid actorUserId, string reason, DateTimeOffset now)
    {
        Require([AssetStatus.InStock, AssetStatus.Assigned, AssetStatus.Maintenance, AssetStatus.Lost], nameof(Retire));
        CloseIfActive(active, actorUserId, now);
        return new AssetChange(ChangeStatus(AssetMovementKind.Retired, AssetStatus.Retired, active?.UserId, actorUserId, reason, null, now));
    }

    private AssetMovement ChangeStatus(
        AssetMovementKind kind, AssetStatus target, Guid? userId, Guid actorUserId, string? reason, bool? retained, DateTimeOffset now)
    {
        var from = Status;
        Status = target;
        UpdatedAt = now;
        return AssetMovement.Record(Id, kind, from, target, userId, actorUserId, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), retained, now);
    }

    private void Require(AssetStatus allowed, string operation) => Require([allowed], operation);

    private void Require(AssetStatus[] allowed, string operation)
    {
        if (!allowed.Contains(Status))
        {
            throw InvalidTransition(Status, operation);
        }
    }

    private void CloseOwn(AssetAssignment? active, Guid actorUserId, DateTimeOffset now)
    {
        if (active is null || !active.IsActive)
        {
            throw InvalidTransition(Status, "close assignment");
        }

        EnsureOwn(active);
        active.Close(actorUserId, now);
    }

    private void CloseIfActive(AssetAssignment? active, Guid actorUserId, DateTimeOffset now)
    {
        if (active is { IsActive: true })
        {
            CloseOwn(active, actorUserId, now);
        }
    }

    private void EnsureOwn(AssetAssignment assignment)
    {
        if (assignment.AssetId != Id)
        {
            throw InvalidTransition(Status, "foreign assignment");
        }
    }

    private static DomainException InvalidTransition(AssetStatus status, string operation) =>
        new(ErrorCodes.AssetInvalidTransition, $"Operation '{operation}' is not allowed in status {status}.");

    private static string RemoveWhitespace(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
}
