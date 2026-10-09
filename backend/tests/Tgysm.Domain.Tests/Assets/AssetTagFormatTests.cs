using Tgysm.Domain.Assets;

namespace Tgysm.Domain.Tests.Assets;

public sealed class AssetTagFormatTests
{
    [Theory]
    [InlineData(1L, "DMR-000001")]
    [InlineData(42L, "DMR-000042")]
    [InlineData(1234567L, "DMR-1234567")]
    public void Generated_tags_are_the_prefix_and_a_zero_padded_number_never_truncated(long number, string expected) =>
        Assert.Equal(expected, AssetTagFormat.Format(number));

    [Theory]
    [InlineData("DMR-000005", true)]
    [InlineData(" dmr-000005 ", true)]
    [InlineData("dmr 12", false)]
    [InlineData("DT-0001", false)]
    [InlineData("dmrbs555555", false)]
    public void The_generated_prefix_is_reserved_for_the_system(string tag, bool reserved) =>
        Assert.Equal(reserved, AssetTagFormat.IsReserved(tag));

    [Fact]
    public void Editing_an_asset_keeps_its_tag()
    {
        var model = new AssetCatalogEntry(Guid.NewGuid(), AssetType.Laptop, "Lenovo", "T14");
        var now = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
        var (asset, _) = Asset.Create("DMR-000001", model, null, Guid.NewGuid(), null, null, null, null, now);

        asset.Update(model, "SN-1", Guid.NewGuid(), null, null, null, null, Guid.NewGuid(), now);

        Assert.Equal("DMR-000001", asset.AssetTag);
    }
}
