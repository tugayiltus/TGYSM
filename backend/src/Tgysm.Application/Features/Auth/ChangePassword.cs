using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Auth;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword)
            .MustBeStrongPassword()
            .NotEqual(c => c.CurrentPassword).WithErrorCode(ErrorCodes.PasswordSameAsOld);
    }
}

/// <summary>Changes the signed-in user's password; rotates the security stamp so other sessions end.</summary>
public sealed class ChangePasswordHandler(
    IAppDbContext db,
    IValidator<ChangePasswordCommand> validator,
    IPasswordService passwords,
    ICurrentActor actor,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<SessionUser> HandleAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new AuthenticationFailedException();

        if (!passwords.Verify(user.PasswordHash, command.CurrentPassword))
        {
            throw new ValidationException(
            [
                new ValidationFailure(nameof(ChangePasswordCommand.CurrentPassword), "Current password is invalid.")
                {
                    ErrorCode = ErrorCodes.CurrentPasswordInvalid,
                },
            ]);
        }

        user.ChangePassword(passwords.Hash(command.NewPassword), clock.GetUtcNow());
        audit.Record(AuditActions.PasswordChanged, AuditEntityTypes.User, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return SessionUser.From(user);
    }
}
