using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Users;

public sealed record ResetUserPasswordCommand(Guid Id);

/// <summary>Admin-issued temporary password (replaces "forgot password" until email arrives in Phase 4).</summary>
public sealed class ResetUserPasswordHandler(
    IAppDbContext db,
    IPasswordService passwords,
    ITemporaryPasswordGenerator temporaryPasswords,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<TemporaryPasswordResult> HandleAsync(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);

        var temporaryPassword = temporaryPasswords.Generate();
        user.SetTemporaryPassword(passwords.Hash(temporaryPassword), clock.GetUtcNow());
        audit.Record(AuditActions.UserPasswordReset, AuditEntityTypes.User, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return new TemporaryPasswordResult(user.Id, temporaryPassword);
    }
}
