using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Users;

public sealed record UpdateUserCommand(
    Guid Id,
    uint Version,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? DepartmentId,
    Guid? LocationId);

internal sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(c => c.FirstName).ValidPersonName();
        RuleFor(c => c.LastName).ValidPersonName();
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Role).IsInEnum();
    }
}

public sealed class UpdateUserHandler(
    IAppDbContext db,
    IValidator<UpdateUserCommand> validator,
    OrganizationReferences references,
    LastAdminGuard lastAdmin,
    PasswordTokenIssuer tokens,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<UserDetail> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        db.SetExpectedVersion(user, command.Version);

        var normalizedEmail = User.NormalizeEmail(command.Email);
        if (normalizedEmail != user.NormalizedEmail
            && await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != user.Id, cancellationToken))
        {
            throw new DomainException(ErrorCodes.EmailTaken, UserMessages.EmailTaken);
        }

        await references.EnsureActiveAsync(
            command.DepartmentId == user.DepartmentId ? null : command.DepartmentId,
            command.LocationId == user.LocationId ? null : command.LocationId,
            cancellationToken);

        await db.RunSerializedAsync(SerializationLocks.AdminMembership, async ct =>
        {
            if (user is { IsActive: true, Role: UserRole.Admin } && command.Role != UserRole.Admin)
            {
                await lastAdmin.EnsureAnotherActiveAdminAsync(user.Id, ct);
            }

            var before = UserAuditSnapshot.From(user);
            var now = clock.GetUtcNow();
            var emailChanged = normalizedEmail != user.NormalizedEmail;
            user.UpdateProfile(command.FirstName, command.LastName, command.Email, command.DepartmentId, command.LocationId, now);
            if (emailChanged)
            {
                // A link mailed to the old address must not keep working once the account moves to a new one.
                await tokens.RevokeUnusedAsync(user.Id, ct);
            }

            user.ChangeRole(command.Role, now);
            var after = UserAuditSnapshot.From(user);

            audit.Record(AuditActions.UserUpdated, AuditEntityTypes.User, user.Id, before, after);
            if (before.Role != after.Role)
            {
                audit.Record(AuditActions.UserRoleChanged, AuditEntityTypes.User, user.Id, new { before.Role }, new { after.Role });
            }

            await db.SaveTranslatingDuplicateAsync(ErrorCodes.EmailTaken, UserMessages.EmailTaken, ct);
        }, cancellationToken);

        return UserMapping.ToDetail(user);
    }
}
