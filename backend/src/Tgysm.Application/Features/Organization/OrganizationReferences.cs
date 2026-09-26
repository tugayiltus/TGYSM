using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization;

/// <summary>Checks that referenced departments and locations exist and are active.</summary>
public sealed class OrganizationReferences(IAppDbContext db)
{
    /// <summary>Pass null for a reference that did not change; only new references are checked.</summary>
    public async Task EnsureActiveAsync(Guid? departmentId, Guid? locationId, CancellationToken cancellationToken)
    {
        if (departmentId is { } department && !await db.Departments.AnyAsync(d => d.Id == department && d.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "Department is missing or inactive.");
        }

        if (locationId is { } location && !await db.Locations.AnyAsync(l => l.Id == location && l.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "Location is missing or inactive.");
        }
    }
}
