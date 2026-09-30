using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Users;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth;

public sealed record ForgotPasswordCommand(string Email);

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(c => c.Email).ValidEmail();
}

/// <summary>
/// Self-service reset request (spec §4, ADR-0017 amendment 2026-10-01). The request path never looks the account
/// up: every well-formed email costs the same one outbox insert, so neither the response nor its timing reveals
/// whether the account exists. The dispatcher resolves the email and issues a reset link only for an active account.
/// A malformed email is dropped without a write; that depends on the input alone, never on an account.
/// </summary>
public sealed class ForgotPasswordHandler(IAppDbContext db, IValidator<ForgotPasswordCommand> validator, INotificationOutbox outbox)
{
    public async Task HandleAsync(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        if (!(await validator.ValidateAsync(command, cancellationToken)).IsValid)
        {
            return;
        }

        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OutboxEvent.RequestedEmailKey] = User.NormalizeEmail(command.Email),
        };
        outbox.Add(InternalEventTypes.PasswordResetRequested, ticketId: null, userId: null, payload);
        await db.SaveChangesAsync(cancellationToken);
    }
}
