using Tgysm.Domain.Common;

namespace Tgysm.Domain.Assets;

/// <summary>Admin-managed device brand; assets pick it through an <see cref="AssetModel"/> (ADR-0023).</summary>
public sealed class Brand : Entity
{
    private Brand()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Brand Create(string name, DateTimeOffset now)
    {
        var brand = new Brand { IsActive = true, CreatedAt = now };
        brand.Rename(name, now);
        return brand;
    }

    public void Rename(string name, DateTimeOffset now)
    {
        Name = CatalogName.Clean(name);
        NormalizedName = CatalogName.Normalize(name);
        UpdatedAt = now;
    }

    /// <summary>A used brand is never deleted; inactive brands stay readable on existing assets and are not offered for new ones.</summary>
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
