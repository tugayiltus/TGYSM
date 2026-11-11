using Tgysm.Domain.Assets;

namespace Tgysm.Domain.Tests.Assets;

public sealed class AssetReportableTests
{
    [Theory]
    [InlineData(AssetStatus.InStock, true)]
    [InlineData(AssetStatus.Assigned, true)]
    [InlineData(AssetStatus.Maintenance, true)]
    [InlineData(AssetStatus.Lost, false)]
    [InlineData(AssetStatus.Retired, false)]
    public void A_new_incident_may_name_a_device_unless_it_is_lost_or_retired(AssetStatus status, bool expected) =>
        Assert.Equal(expected, Asset.CanBeReported(status));
}
