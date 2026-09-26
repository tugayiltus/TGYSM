using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Locations;

public sealed record SetLocationActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed class SetLocationActiveHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task<LocationItem> HandleAsync(SetLocationActiveCommand command, CancellationToken cancellationToken)
    {
        var location = await db.Locations.SingleOrDefaultAsync(l => l.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Location);
        if (location.IsActive == command.IsActive)
        {
            return LocationItem.From(location);
        }

        db.SetExpectedVersion(location, command.Version);
        if (command.IsActive)
        {
            await LocationRules.EnsureParentActiveAsync(db, location.ParentId, cancellationToken);
            location.Reactivate();
        }
        else
        {
            if (await db.Locations.AnyAsync(l => l.ParentId == location.Id && l.IsActive, cancellationToken))
            {
                throw new DomainException(ErrorCodes.LocationHasActiveChildren, "Deactivate child locations first.");
            }

            location.Deactivate();
        }

        audit.Record(
            command.IsActive ? AuditActions.LocationReactivated : AuditActions.LocationDeactivated,
            AuditEntityTypes.Location,
            location.Id);
        await db.SaveChangesAsync(cancellationToken);
        return LocationItem.From(location);
    }
}
