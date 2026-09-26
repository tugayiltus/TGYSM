using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Organization.Departments;

public sealed record SetDepartmentActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed class SetDepartmentActiveHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task<DepartmentItem> HandleAsync(SetDepartmentActiveCommand command, CancellationToken cancellationToken)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Department);
        if (department.IsActive != command.IsActive)
        {
            db.SetExpectedVersion(department, command.Version);
            if (command.IsActive)
            {
                department.Reactivate();
            }
            else
            {
                department.Deactivate();
            }

            audit.Record(
                command.IsActive ? AuditActions.DepartmentReactivated : AuditActions.DepartmentDeactivated,
                AuditEntityTypes.Department,
                department.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await DepartmentQueries.LoadItemAsync(db, department.Id, cancellationToken);
    }
}
