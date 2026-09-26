using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

/// <summary>The system must always keep at least one active admin (ADR-0005).</summary>
public sealed class LastAdminGuard(IAppDbContext db)
{
    public async Task EnsureAnotherActiveAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        var anotherAdminExists = await db.Users.AnyAsync(
            u => u.Id != userId && u.IsActive && u.Role == UserRole.Admin, cancellationToken);
        if (!anotherAdminExists)
        {
            throw new DomainException(ErrorCodes.LastAdmin, "The last active admin cannot be demoted or deactivated.");
        }
    }
}
