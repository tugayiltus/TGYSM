using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record UserListItem(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? LocationId,
    string? LocationName,
    uint Version);

public sealed record UserDetail(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword,
    Guid? DepartmentId,
    Guid? LocationId,
    DateTimeOffset CreatedAt,
    uint Version);

/// <summary>
/// <paramref name="TemporaryPassword"/> is shown to the admin once and never stored in plain text; it is null when
/// the credentials went out as an email link (<see cref="CredentialDelivery.EmailLink"/>).
/// </summary>
public sealed record TemporaryPasswordResult(Guid UserId, string? TemporaryPassword);

/// <summary>Audit snapshot; deliberately excludes password hash and security stamp.</summary>
internal sealed record UserAuditSnapshot(
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? DepartmentId,
    Guid? LocationId,
    bool IsActive)
{
    public static UserAuditSnapshot From(User user) =>
        new(user.FirstName, user.LastName, user.Email, user.Role, user.DepartmentId, user.LocationId, user.IsActive);
}
