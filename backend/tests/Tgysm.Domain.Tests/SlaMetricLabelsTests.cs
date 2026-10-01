using Tgysm.Domain.Notifications;
using Tgysm.Domain.Sla;

namespace Tgysm.Domain.Tests;

public sealed class SlaMetricLabelsTests
{
    [Theory]
    [InlineData(SlaMetric.FirstResponse, "first_response", "ilk yanıt")]
    [InlineData(SlaMetric.Resolution, "resolution", "çözüm")]
    public void Key_is_stored_and_label_is_turkish(SlaMetric metric, string key, string label)
    {
        Assert.Equal(key, SlaMetricLabels.KeyOf(metric));
        Assert.Equal(label, SlaMetricLabels.LabelOfKey(key));
    }

    [Fact]
    public void Every_metric_has_a_key_with_a_real_label() =>
        Assert.All(Enum.GetValues<SlaMetric>(), m => Assert.NotEqual(SlaMetricLabels.KeyOf(m), SlaMetricLabels.LabelOfKey(SlaMetricLabels.KeyOf(m))));
}
