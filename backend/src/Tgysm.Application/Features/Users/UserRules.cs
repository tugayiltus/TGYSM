using FluentValidation;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

internal static class UserRules
{
    public static IRuleBuilderOptions<T, string> ValidPersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(User.EmailMaxLength).EmailAddress();
}

internal static class UserMessages
{
    public const string EmailTaken = "Another account uses this email.";
}
