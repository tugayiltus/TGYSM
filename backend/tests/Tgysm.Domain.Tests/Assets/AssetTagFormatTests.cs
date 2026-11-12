using Tgysm.Domain.Assets;

namespace Tgysm.Domain.Tests.Assets;

public sealed class AssetTagFormatTests
{
    [Theory]
    [InlineData(2026, 1L, "ASSET-2026-0001")]
    [InlineData(2026, 42L, "ASSET-2026-0042")]
    [InlineData(2027, 9999L, "ASSET-2027-9999")]
    [InlineData(2027, 12345L, "ASSET-2027-12345")]
    public void Generated_tags_are_the_prefix_the_year_and_a_number_of_at_least_four_digits(int year, long number, string expected) =>
        Assert.Equal(expected, AssetTagFormat.Format(year, number));

    [Fact]
    public void The_year_is_the_company_calendar_year_of_the_moment()
    {
        var istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        // 22:30 UTC on 31 December is already 1 January in Istanbul (UTC+3).
        Assert.Equal(2027, AssetTagFormat.YearOf(new DateTimeOffset(2026, 12, 31, 22, 30, 0, TimeSpan.Zero), istanbul));
    }

    [Theory]
    [InlineData("ASSET-2026-0005", true)]
    [InlineData(" asset-2026-12345 ", true)]
    [InlineData("DMR-000005", false)]
    [InlineData("dmrbs555555", false)]
    [InlineData("ASSET-26-0001", false)]
    [InlineData("ASSET-2026-001", false)]
    public void Only_the_generated_format_counts_as_current(string tag, bool current) =>
        Assert.Equal(current, AssetTagFormat.IsCurrent(tag));

    [Fact]
    public void Editing_an_asset_keeps_its_tag()
    {
        var model = new AssetCatalogEntry(Guid.NewGuid(), AssetType.Laptop, "Lenovo", "T14");
        var now = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
        var (asset, _) = Asset.Create("ASSET-2026-0001", model, null, Guid.NewGuid(), null, null, null, null, now);

        asset.Update(model, "SN-1", Guid.NewGuid(), null, null, null, null, Guid.NewGuid(), now);

        Assert.Equal("ASSET-2026-0001", asset.AssetTag);
    }
}
