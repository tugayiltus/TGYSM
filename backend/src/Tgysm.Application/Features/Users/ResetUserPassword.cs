using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record ResetUserPasswordCommand(Guid Id, CredentialDelivery Delivery = CredentialDelivery.EmailLink);

internal sealed class ResetUserPasswordValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordValidator() => RuleFor(c => c.Delivery).IsInEnum();
}

/// <summary>
/// Admin-initiated reset (spec §4): <see cref="CredentialDelivery.EmailLink"/> queues a reset link and leaves the
/// current password in place until the link is used; <see cref="CredentialDelivery.TemporaryPassword"/> replaces
/// it with a temporary password shown once (ADR-0004/ADR-0007 behavior).
/// </summary>
public sealed class ResetUserPasswordHandler(
    IAppDbContext db,
    IValidator<ResetUserPasswordCommand> validator,
    IPasswordService passwords,
    ITemporaryPasswordGenerator temporaryPasswords,
    PasswordTokenIssuer tokens,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<TemporaryPasswordResult> HandleAsync(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);

        if (command.Delivery == CredentialDelivery.EmailLink)
        {
            await tokens.IssueAsync(user.Id, PasswordTokenPurpose.Reset, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return new TemporaryPasswordResult(user.Id, TemporaryPassword: null);
        }

        var temporaryPassword = temporaryPasswords.Generate();
        user.SetTemporaryPassword(passwords.Hash(temporaryPassword), clock.GetUtcNow());
        audit.Record(AuditActions.UserPasswordReset, AuditEntityTypes.User, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return new TemporaryPasswordResult(user.Id, temporaryPassword);
    }
}
