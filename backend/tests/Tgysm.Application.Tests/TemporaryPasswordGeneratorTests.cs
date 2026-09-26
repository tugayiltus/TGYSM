using Tgysm.Application.Security;

namespace Tgysm.Application.Tests;

public sealed class TemporaryPasswordGeneratorTests
{
    private const int SampleCount = 200;

    [Fact]
    public void Generates_passwords_that_satisfy_the_policy_and_differ()
    {
        var generator = new TemporaryPasswordGenerator();

        var passwords = Enumerable.Range(0, SampleCount).Select(_ => generator.Generate()).ToList();

        Assert.All(passwords, p =>
        {
            Assert.Equal(TemporaryPasswordGenerator.Length, p.Length);
            Assert.True(p.Length >= PasswordRules.MinLength);
            Assert.Contains(p, char.IsLetter);
            Assert.Contains(p, char.IsDigit);
        });
        Assert.Equal(SampleCount, passwords.Distinct().Count());
    }
}
