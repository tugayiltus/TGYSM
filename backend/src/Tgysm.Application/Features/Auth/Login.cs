using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password);

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty().MaximumLength(User.EmailMaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}

/// <summary>
/// Verifies credentials. Every failure throws <see cref="AuthenticationFailedException"/> so callers cannot tell
/// unknown, inactive, locked and wrong-password accounts apart.
/// </summary>
public sealed class LoginHandler(
    IAppDbContext db,
    IValidator<LoginCommand> validator,
    IPasswordService passwords,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<SessionUser> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var now = clock.GetUtcNow();
        var normalizedEmail = User.NormalizeEmail(command.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            // Same hashing cost as a real check, so response time does not reveal unknown emails.
            passwords.Verify(passwords.DummyHash, command.Password);
            audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, entityId: null, newValues: new { Email = normalizedEmail });
            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        if (!user.CanSignIn(now))
        {
            // Same hashing cost as a real check: locked or inactive accounts must not answer faster.
            passwords.Verify(user.PasswordHash, command.Password);
            audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, user.Id);
            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        if (!passwords.Verify(user.PasswordHash, command.Password))
        {
            var lockedNow = await RegisterFailedSignInAsync(user.Id, now, cancellationToken);
            audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, user.Id);
            if (lockedNow)
            {
                audit.Record(AuditActions.LockedOut, AuditEntityTypes.User, user.Id);
            }

            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(u => u.AccessFailedCount, 0)
                .SetProperty(u => u.LockoutEnd, (DateTimeOffset?)null)
                .SetProperty(u => u.UpdatedAt, now),
            cancellationToken);
        audit.Record(AuditActions.LoginSucceeded, AuditEntityTypes.User, user.Id, actorUserId: user.Id);
        await db.SaveChangesAsync(cancellationToken);
        return SessionUser.From(user);
    }

    /// <summary>
    /// Counts a failed sign-in in one UPDATE, so parallel attempts cannot overwrite each other's count
    /// (the tracked entity would lose updates). Returns true when this attempt started the lockout.
    /// </summary>
    private async Task<bool> RegisterFailedSignInAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const int lockThreshold = User.MaxFailedSignInAttempts - 1;
        var lockoutEnd = TruncateToMicroseconds(now + User.LockoutDuration);

        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(u => u.LockoutEnd, u => u.AccessFailedCount >= lockThreshold ? lockoutEnd : u.LockoutEnd)
                .SetProperty(u => u.AccessFailedCount, u => u.AccessFailedCount >= lockThreshold ? 0 : u.AccessFailedCount + 1)
                .SetProperty(u => u.UpdatedAt, now),
            cancellationToken);

        return await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.LockoutEnd == lockoutEnd, cancellationToken);
    }

    /// <summary>PostgreSQL timestamps keep microseconds; the value read back must compare equal.</summary>
    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
}
