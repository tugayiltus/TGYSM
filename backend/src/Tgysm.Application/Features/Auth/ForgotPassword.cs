using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Security;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth;

public sealed record ForgotPasswordCommand(string Email);

/// <summary>
/// Self-service reset request (spec §4): only an active account gets a reset link queued. The caller answers the
/// same way whatever happens here, so the response never reveals whether the account exists.
/// </summary>
public sealed class ForgotPasswordHandler(IAppDbContext db, PasswordTokenIssuer issuer)
{
    public async Task HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
        {
            return;
        }

        var normalizedEmail = User.NormalizeEmail(command.Email);
        var userId = await db.Users
            .Where(u => u.NormalizedEmail == normalizedEmail && u.IsActive)
            .Select(u => (Guid?)u.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is not { } id)
        {
            return;
        }

        await issuer.IssueAsync(id, PasswordTokenPurpose.Reset, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
