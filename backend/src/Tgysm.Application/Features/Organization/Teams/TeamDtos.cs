using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Organization.Teams;

public sealed record TeamItem(Guid Id, string Name, bool IsActive, bool IsDefaultIntake, int MemberCount, uint Version);

public sealed record TeamMemberItem(Guid UserId, string FirstName, string LastName, string Email, UserRole Role, bool IsActive);

public sealed record TeamDetail(Guid Id, string Name, bool IsActive, bool IsDefaultIntake, uint Version, IReadOnlyList<TeamMemberItem> Members);
