using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Users;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Setup;

public sealed record SetupFirstAdminCommand(string Email, string FirstName, string LastName);

public sealed record SetupFirstAdminResult(Guid UserId, string TemporaryPassword);

internal sealed class SetupFirstAdminValidator : AbstractValidator<SetupFirstAdminCommand>
{
    public SetupFirstAdminValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.FirstName).ValidPersonName();
        RuleFor(c => c.LastName).ValidPersonName();
    }
}

/// <summary>Creates the first admin once (ADR-0007). Refuses when any account exists.</summary>
public sealed class SetupFirstAdminHandler(
    IAppDbContext db,
    IValidator<SetupFirstAdminCommand> validator,
    IPasswordService passwords,
    ITemporaryPasswordGenerator temporaryPasswords,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<SetupFirstAdminResult> HandleAsync(SetupFirstAdminCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            throw new DomainException(ErrorCodes.SetupAlreadyCompleted, "An account already exists; setup is closed.");
        }

        var temporaryPassword = temporaryPasswords.Generate();
        var admin = User.Create(
            command.FirstName,
            command.LastName,
            command.Email,
            UserRole.Admin,
            departmentId: null,
            locationId: null,
            passwords.Hash(temporaryPassword),
            clock.GetUtcNow());

        db.Users.Add(admin);
        audit.Record(
            AuditActions.FirstAdminCreated,
            AuditEntityTypes.User,
            admin.Id,
            newValues: new { admin.Email, admin.Role },
            actorUserId: admin.Id);
        await db.SaveChangesAsync(cancellationToken);

        return new SetupFirstAdminResult(admin.Id, temporaryPassword);
    }
}
