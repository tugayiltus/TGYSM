using FluentValidation;
using FluentValidation.Results;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.Http;

internal static class QueryParsing
{
    private const string RoleParameter = "role";

    /// <summary>Parses the snake_case role name used in URLs ("it_staff"); unknown names are a 400.</summary>
    public static UserRole? ParseRole(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return UserRoleNames.TryParse(value, out var role)
            ? role
            : throw new ValidationException([new ValidationFailure(RoleParameter, "Unknown role.") { ErrorCode = ErrorCodes.InvalidValue }]);
    }
}
