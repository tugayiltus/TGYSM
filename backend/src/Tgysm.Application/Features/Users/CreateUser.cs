using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Organization;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record CreateUserCommand(
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? DepartmentId,
    Guid? LocationId,
    CredentialDelivery Delivery = CredentialDelivery.EmailLink);

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.FirstName).ValidPersonName();
        RuleFor(c => c.LastName).ValidPersonName();
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Role).IsInEnum();
        RuleFor(c => c.Delivery).IsInEnum();
    }
}

/// <summary>
/// Creates an account (spec §4): with <see cref="CredentialDelivery.EmailLink"/> the stored password is a random one
/// nobody sees and an activation link is queued; with <see cref="CredentialDelivery.TemporaryPassword"/> the
/// temporary password is returned once.
/// </summary>
public sealed class CreateUserHandler(
    IAppDbContext db,
    IValidator<CreateUserCommand> validator,
    OrganizationReferences references,
    IPasswordService passwords,
    ITemporaryPasswordGenerator temporaryPasswords,
    PasswordTokenIssuer tokens,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<TemporaryPasswordResult> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedEmail = User.NormalizeEmail(command.Email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new DomainException(ErrorCodes.EmailTaken, UserMessages.EmailTaken);
        }

        await references.EnsureActiveAsync(command.DepartmentId, command.LocationId, cancellationToken);

        var temporaryPassword = temporaryPasswords.Generate();
        var user = User.Create(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Role,
            command.DepartmentId,
            command.LocationId,
            passwords.Hash(temporaryPassword),
            clock.GetUtcNow());

        db.Users.Add(user);
        audit.Record(AuditActions.UserCreated, AuditEntityTypes.User, user.Id, newValues: UserAuditSnapshot.From(user));
        if (command.Delivery == CredentialDelivery.EmailLink)
        {
            await tokens.IssueAsync(user.Id, PasswordTokenPurpose.Activation, cancellationToken);
        }

        // A parallel request with the same email loses on the unique index (double submit).
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.EmailTaken, UserMessages.EmailTaken, cancellationToken);
        return new TemporaryPasswordResult(user.Id, command.Delivery == CredentialDelivery.TemporaryPassword ? temporaryPassword : null);
    }
}
