using FluentValidation;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Security;

/// <summary>Password policy (ADR-0004): 12–128 characters, at least one letter and one digit.</summary>
public static class PasswordRules
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    public static IRuleBuilderOptions<T, string> MustBeStrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(IsStrong)
            .WithErrorCode(ErrorCodes.PasswordPolicy)
            .WithMessage($"Password must be {MinLength}-{MaxLength} characters and contain a letter and a digit.");

    private static bool IsStrong(string? password) =>
        password is { Length: >= MinLength and <= MaxLength }
        && password.Any(char.IsLetter)
        && password.Any(char.IsDigit);
}
