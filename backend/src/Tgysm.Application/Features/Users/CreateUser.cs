using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record CreateUserCommand(string FirstName, string LastName, string Email, UserRole Role, Guid? DepartmentId, Guid? LocationId);

internal sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.FirstName).ValidPersonName();
        RuleFor(c => c.LastName).ValidPersonName();
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Role).IsInEnum();
    }
}

public sealed class CreateUserHandler(
    IAppDbContext db,
    IValidator<CreateUserCommand> validator,
    OrganizationReferences references,
    IPasswordService passwords,
    ITemporaryPasswordGenerator temporaryPasswords,
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

        // A parallel request with the same email loses on the unique index (double submit).
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.EmailTaken, UserMessages.EmailTaken, cancellationToken);
        return new TemporaryPasswordResult(user.Id, temporaryPassword);
    }
}
