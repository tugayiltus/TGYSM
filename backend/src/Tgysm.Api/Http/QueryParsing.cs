using FluentValidation;
using FluentValidation.Results;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.Http;

internal static class QueryParsing
{
    private const string RoleParameter = "role";

    /// <summary>Parses a snake_case enum name from a query string ("in_stock"); unknown names are a 400.</summary>
    public static TEnum? ParseEnum<TEnum>(string? value, string parameter)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(candidate.ToString()), value, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        throw new ValidationException([new ValidationFailure(parameter, "Unknown value.") { ErrorCode = ErrorCodes.InvalidValue }]);
    }

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
