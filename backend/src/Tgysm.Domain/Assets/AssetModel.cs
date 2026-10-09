using Tgysm.Domain.Common;

namespace Tgysm.Domain.Assets;

/// <summary>Admin-managed model of a brand for one device type; unique by brand, type and normalized name (ADR-0023).</summary>
public sealed class AssetModel : Entity
{
    private AssetModel()
    {
    }

    public Guid BrandId { get; private set; }
    public AssetType Type { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static AssetModel Create(Guid brandId, AssetType type, string name, DateTimeOffset now)
    {
        var model = new AssetModel { BrandId = brandId, Type = type, IsActive = true, CreatedAt = now };
        model.Rename(name, now);
        return model;
    }

    public void Rename(string name, DateTimeOffset now)
    {
        Name = CatalogName.Clean(name);
        NormalizedName = CatalogName.Normalize(name);
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    public void Reactivate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
    }
}
