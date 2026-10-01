using Tgysm.Domain.Common;
using Tgysm.Domain.Sla;

namespace Tgysm.Domain.Tests;

public sealed class SlaCalendarTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Europe/Istanbul", true)]
    [InlineData("America/New_York", true)]
    [InlineData("UTC", true)]
    [InlineData("Turkey Standard Time", false)]
    [InlineData("Mars/Olympus", false)]
    [InlineData("EST", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    public void IanaTimeZone_accepts_only_known_IANA_identifiers(string id, bool expected) =>
        Assert.Equal(expected, IanaTimeZone.IsValid(id));

    [Fact]
    public void Calendar_Update_rejects_a_non_IANA_zone()
    {
        var calendar = BusinessCalendar.Create("Europe/Istanbul", Guid.NewGuid(), Now);

        var exception = Assert.Throws<DomainException>(() => calendar.Update("Turkey Standard Time", Guid.NewGuid(), Now));

        Assert.Equal(ErrorCodes.SlaInvalidTimeZone, exception.Code);
    }

    [Fact]
    public void BusinessHours_rejects_an_inverted_range()
    {
        var exception = Assert.Throws<DomainException>(() =>
            BusinessHours.Create(DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(9, 0)));

        Assert.Equal(ErrorCodes.SlaInvalidHours, exception.Code);
    }
}
