using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Departments;

public sealed record UpdateDepartmentCommand(Guid Id, uint Version, string Name, Guid? ApproverUserId);

internal sealed class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator() => RuleFor(c => c.Name).ValidDepartmentName();
}

public sealed class UpdateDepartmentHandler(IAppDbContext db, IValidator<UpdateDepartmentCommand> validator, IAuditLog audit)
{
    public async Task<DepartmentItem> HandleAsync(UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Department);
        db.SetExpectedVersion(department, command.Version);

        if (TurkishText.ToSearchKey(command.Name) != department.NormalizedName)
        {
            await DepartmentQueries.EnsureNameFreeAsync(db, command.Name, department.Id, cancellationToken);
        }

        if (command.ApproverUserId != department.ApproverUserId)
        {
            await DepartmentQueries.EnsureApproverAsync(db, command.ApproverUserId, cancellationToken);
        }

        var before = new { department.Name, department.ApproverUserId };
        department.Update(command.Name, command.ApproverUserId);
        audit.Record(AuditActions.DepartmentUpdated, AuditEntityTypes.Department, department.Id, before,
            new { department.Name, department.ApproverUserId });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.DepartmentNameTaken, DepartmentQueries.NameTakenMessage, cancellationToken);
        return await DepartmentQueries.LoadItemAsync(db, department.Id, cancellationToken);
    }
}
