using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Features.Auth;

public sealed record GetSessionQuery(Guid UserId);

/// <summary>Returns the session data of an active user, or null when the user is missing or inactive.</summary>
public sealed class GetSessionHandler(IAppDbContext db)
{
    public Task<SessionUser?> HandleAsync(GetSessionQuery query, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == query.UserId && u.IsActive)
            .Select(u => new SessionUser(u.Id, u.Role, u.SecurityStamp, u.MustChangePassword))
            .SingleOrDefaultAsync(cancellationToken);
}
