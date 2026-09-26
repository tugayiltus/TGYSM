using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth;

/// <summary>What the session cookie needs about a user; re-read on every request (ADR-0004).</summary>
public sealed record SessionUser(Guid UserId, UserRole Role, string SecurityStamp, bool MustChangePassword)
{
    public static SessionUser From(User user) => new(user.Id, user.Role, user.SecurityStamp, user.MustChangePassword);
}
