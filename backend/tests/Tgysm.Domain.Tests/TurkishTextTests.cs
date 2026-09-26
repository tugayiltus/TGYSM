using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tests;

public sealed class TurkishTextTests
{
    [Theory]
    [InlineData("İLKER IŞIK", "ilker işik")]
    [InlineData("IT Destek", "it destek")]
    [InlineData("it destek", "it destek")]
    [InlineData("  İstanbul  ", "istanbul")]
    [InlineData("ÇAĞRI ÖZGÜR", "çağri özgür")]
    public void ToSearchKey_lowercases_with_turkish_rules_and_trims(string input, string expected) =>
        Assert.Equal(expected, TurkishText.ToSearchKey(input));
}
