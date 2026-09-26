using FluentValidation;
using Tgysm.Application.Security;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Tests;

public sealed class PasswordRulesTests
{
    private sealed record Input(string Password);

    private sealed class InputValidator : AbstractValidator<Input>
    {
        public InputValidator() => RuleFor(i => i.Password).MustBeStrongPassword();
    }

    private readonly InputValidator _validator = new();

    [Theory]
    [InlineData("short1a")]
    [InlineData("onlyletterslong")]
    [InlineData("123456789012")]
    public void Rejects_weak_passwords(string password)
    {
        var result = _validator.Validate(new Input(password));

        Assert.Contains(result.Errors, e => e.ErrorCode == ErrorCodes.PasswordPolicy);
    }

    [Fact]
    public void Rejects_password_longer_than_maximum() =>
        Assert.False(_validator.Validate(new Input(new string('a', PasswordRules.MaxLength) + "1")).IsValid);

    [Fact]
    public void Accepts_twelve_characters_with_letter_and_digit() =>
        Assert.True(_validator.Validate(new Input("abcdefghijk1")).IsValid);
}
