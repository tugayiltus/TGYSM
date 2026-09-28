using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests;

/// <summary>
/// <see cref="TicketStatusLabels"/> — the single Turkish source for the <c>status</c> email variable (review fix
/// round 1). Every <see cref="TicketStatus"/> value must have a real label, not the raw enum name.
/// </summary>
public sealed class TicketStatusLabelsTests
{
    [Theory]
    [InlineData(TicketStatus.Open, "Açık")]
    [InlineData(TicketStatus.InProgress, "İşlemde")]
    [InlineData(TicketStatus.OnHold, "Beklemede")]
    [InlineData(TicketStatus.Completed, "Tamamlandı")]
    [InlineData(TicketStatus.Cancelled, "İptal edildi")]
    public void Of_returns_the_turkish_label(TicketStatus status, string expected) =>
        Assert.Equal(expected, TicketStatusLabels.Of(status));

    [Fact]
    public void Every_ticket_status_value_has_a_label_that_is_not_the_raw_enum_name()
    {
        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            Assert.NotEqual(status.ToString(), TicketStatusLabels.Of(status));
        }
    }
}
