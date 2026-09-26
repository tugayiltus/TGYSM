using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Converters;

internal static class UserRoleConversion
{
    public const int MaxLength = 20;

    public static readonly ValueConverter<UserRole, string> Converter = new(
        role => UserRoleNames.ToName(role),
        name => Parse(name));

    private static UserRole Parse(string name) =>
        UserRoleNames.TryParse(name, out var role)
            ? role
            : throw new InvalidOperationException($"Unknown role '{name}' in database.");
}
