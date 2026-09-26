using System.Diagnostics.CodeAnalysis;

namespace Tgysm.Domain.Users;

/// <summary>Canonical role names used in the database, JSON and claims (ADR-0005).</summary>
public static class UserRoleNames
{
    public const string Employee = "employee";
    public const string ItStaff = "it_staff";
    public const string Admin = "admin";

    public static string ToName(UserRole role) => role switch
    {
        UserRole.Employee => Employee,
        UserRole.ItStaff => ItStaff,
        UserRole.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
    };

    public static bool TryParse([NotNullWhen(true)] string? name, out UserRole role)
    {
        switch (name)
        {
            case Employee: role = UserRole.Employee; return true;
            case ItStaff: role = UserRole.ItStaff; return true;
            case Admin: role = UserRole.Admin; return true;
            default: role = default; return false;
        }
    }
}
