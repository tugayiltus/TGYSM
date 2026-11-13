using Tgysm.Domain.Changes;

namespace Tgysm.Domain.Tests.Changes;

public sealed class ChangeNumberTests
{
    [Fact]
    public void Format_pads_to_six_digits() => Assert.Equal("CHG-2026-000007", ChangeNumber.Format(2026, 7));

    [Theory]
    [InlineData("CHG-2026-000007", true)]
    [InlineData("chg-2026-1234567", true)]
    [InlineData("TSK-2026-000007", false)]
    [InlineData("CHG-2026-7", false)]
    public void IsValid_checks_the_pattern(string number, bool expected) => Assert.Equal(expected, ChangeNumber.IsValid(number));

    [Fact]
    public void Normalize_trims_and_upper_cases() => Assert.Equal("CHG-2026-000007", ChangeNumber.Normalize(" chg-2026-000007 "));
}
