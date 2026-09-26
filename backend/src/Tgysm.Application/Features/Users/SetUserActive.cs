using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record SetUserActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed class SetUserActiveHandler(IAppDbContext db, LastAdminGuard lastAdmin, IAuditLog audit, TimeProvider clock)
{
    public async Task<UserDetail> HandleAsync(SetUserActiveCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        if (user.IsActive == command.IsActive)
        {
            return UserMapping.ToDetail(user);
        }

        db.SetExpectedVersion(user, command.Version);
        await db.RunSerializedAsync(SerializationLocks.AdminMembership, async ct =>
        {
            var now = clock.GetUtcNow();
            if (command.IsActive)
            {
                user.Reactivate(now);
            }
            else
            {
                if (user.Role == UserRole.Admin)
                {
                    await lastAdmin.EnsureAnotherActiveAdminAsync(user.Id, ct);
                }

                user.Deactivate(now);
            }

            audit.Record(command.IsActive ? AuditActions.UserReactivated : AuditActions.UserDeactivated, AuditEntityTypes.User, user.Id);
            await db.SaveChangesAsync(ct);
        }, cancellationToken);

        return UserMapping.ToDetail(user);
    }
}
