using Tgysm.Domain.Users;

namespace Tgysm.Api.Contracts;

public sealed record VersionRequest(uint Version);

public sealed record UpdateUserRequest(
    uint Version,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? DepartmentId,
    Guid? LocationId);

public sealed record UpdateDepartmentRequest(uint Version, string Name, Guid? ApproverUserId);

public sealed record UpdateLocationRequest(uint Version, string Name, Guid? ParentId);

public sealed record UpdateTeamRequest(uint Version, string Name, bool? IsDefaultIntake = null);

public sealed record AddTeamMemberRequest(Guid UserId);
