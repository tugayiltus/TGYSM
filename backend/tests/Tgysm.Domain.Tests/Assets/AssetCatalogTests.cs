using Tgysm.Domain.Assets;

namespace Tgysm.Domain.Tests.Assets;

public sealed class AssetCatalogTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(" LENOVO ", "Lenovo")]
    [InlineData("Think  Pad", "think pad")]
    [InlineData("IŞIK", "ışık")]
    public void Names_that_differ_only_in_case_spacing_or_Turkish_i_share_a_key(string left, string right) =>
        Assert.Equal(CatalogName.Normalize(left), CatalogName.Normalize(right));

    [Theory]
    [InlineData("ThinkPad T14", "ThinkPad T14s")]
    [InlineData("Latitude 5440", "Latitude-5440")]
    public void Different_model_names_keep_different_keys(string left, string right) =>
        Assert.NotEqual(CatalogName.Normalize(left), CatalogName.Normalize(right));

    [Fact]
    public void A_brand_is_created_active_with_a_trimmed_name_and_can_be_renamed_and_deactivated()
    {
        var brand = Brand.Create("  Lenovo ", Now);
        Assert.Equal("Lenovo", brand.Name);
        Assert.Equal(CatalogName.Normalize("lenovo"), brand.NormalizedName);
        Assert.True(brand.IsActive);

        brand.Rename("LENOVO Group", Now);
        Assert.Equal("LENOVO Group", brand.Name);
        brand.Deactivate(Now);
        Assert.False(brand.IsActive);
        brand.Reactivate(Now);
        Assert.True(brand.IsActive);
    }

    [Fact]
    public void A_model_belongs_to_a_brand_and_a_device_type()
    {
        var brand = Brand.Create("Lenovo", Now);
        var model = AssetModel.Create(brand.Id, AssetType.Laptop, " ThinkPad T14 ", Now);
        Assert.Equal((brand.Id, AssetType.Laptop, "ThinkPad T14"), (model.BrandId, model.Type, model.Name));
        Assert.True(model.IsActive);
    }

    [Fact]
    public void An_asset_takes_its_type_and_catalog_names_from_the_model()
    {
        var entry = new AssetCatalogEntry(Guid.NewGuid(), AssetType.Phone, "Apple", "iPhone 15");
        var (asset, _) = Asset.Create("DT-1", entry, null, Guid.NewGuid(), null, null, null, null, Now);
        Assert.Equal((entry.ModelId, AssetType.Phone, "Apple", "iPhone 15"), (asset.ModelId, asset.Type, asset.Brand, asset.Model));
    }
}
