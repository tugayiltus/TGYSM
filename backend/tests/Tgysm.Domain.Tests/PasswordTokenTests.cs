using Tgysm.Domain.Users;

namespace Tgysm.Domain.Tests;

public sealed class PasswordTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(PasswordTokenPurpose.Activation, 72)]
    [InlineData(PasswordTokenPurpose.Reset, 1)]
    public void Issue_sets_the_purpose_lifetime(PasswordTokenPurpose purpose, int hours)
    {
        var token = PasswordToken.Issue(Guid.NewGuid(), purpose, "hash", Now);

        Assert.Equal(Now.AddHours(hours), token.ExpiresAt);
        Assert.Equal(Now, token.CreatedAt);
        Assert.Null(token.UsedAt);
    }

    [Fact]
    public void Void_marks_an_unused_token_and_keeps_the_time_of_an_already_used_one()
    {
        var token = PasswordToken.Issue(Guid.NewGuid(), PasswordTokenPurpose.Reset, "hash", Now);

        token.Void(Now.AddMinutes(1));
        token.Void(Now.AddMinutes(2));

        Assert.Equal(Now.AddMinutes(1), token.UsedAt);
    }

    [Fact]
    public void Unknown_purpose_has_no_lifetime() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordToken.LifetimeOf((PasswordTokenPurpose)99));
}
