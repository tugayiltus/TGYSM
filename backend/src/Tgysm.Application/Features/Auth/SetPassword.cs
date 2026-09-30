using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Auth;

public sealed record SetPasswordCommand(string Token, string NewPassword);

internal sealed class SetPasswordValidator : AbstractValidator<SetPasswordCommand>
{
    public SetPasswordValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
        RuleFor(c => c.NewPassword).MustBeStrongPassword();
    }
}

/// <summary>
/// Redeems a password-set link (spec §4, ADR-0017). A single conditional update
/// (<c>used_at IS NULL AND expires_at &gt; now</c>) claims the token, so of two parallel redemptions only one can
/// win; in the same transaction the password is set, <c>must_change_password</c> cleared and the security stamp
/// rotated (ending every other session). Every failure is <see cref="PasswordTokenInvalidException"/>.
/// </summary>
public sealed class SetPasswordHandler(
    IAppDbContext db,
    IValidator<SetPasswordCommand> validator,
    IPasswordService passwords,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task HandleAsync(SetPasswordCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var tokenHash = PasswordTokenIssuer.Hash(command.Token);
        var passwordHash = passwords.Hash(command.NewPassword);
        var now = clock.GetUtcNow();

        await db.RunInTransactionAsync(
            async ct =>
            {
                var token = await db.PasswordTokens.AsNoTracking()
                    .Where(t => t.TokenHash == tokenHash)
                    .Select(t => new { t.Id, t.UserId })
                    .SingleOrDefaultAsync(ct)
                    ?? throw new PasswordTokenInvalidException();

                var claimed = await db.PasswordTokens
                    .Where(t => t.Id == token.Id && t.UsedAt == null && t.ExpiresAt > now)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);
                if (claimed == 0)
                {
                    throw new PasswordTokenInvalidException();
                }

                // Throwing rolls the claim back with everything else: a deactivated account's token stays unused.
                var user = await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId && u.IsActive, ct)
                    ?? throw new PasswordTokenInvalidException();
                user.ChangePassword(passwordHash, now);
                audit.Record(AuditActions.UserPasswordSetByLink, AuditEntityTypes.User, user.Id, actorUserId: user.Id);
                await db.SaveChangesAsync(ct);
            },
            cancellationToken);
    }
}
