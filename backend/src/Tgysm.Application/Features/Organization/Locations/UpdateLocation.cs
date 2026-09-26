using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization.Locations;

public sealed record UpdateLocationCommand(Guid Id, uint Version, string Name, Guid? ParentId);

internal sealed class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
{
    public UpdateLocationValidator() => RuleFor(c => c.Name).ValidLocationName();
}

public sealed class UpdateLocationHandler(IAppDbContext db, IValidator<UpdateLocationCommand> validator, IAuditLog audit)
{
    public async Task<LocationItem> HandleAsync(UpdateLocationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var location = await db.Locations.SingleOrDefaultAsync(l => l.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Location);
        db.SetExpectedVersion(location, command.Version);

        if (command.ParentId == location.Id)
        {
            throw new DomainException(ErrorCodes.LocationCycle, "A location cannot be its own parent.");
        }

        await db.RunSerializedAsync(SerializationLocks.LocationTree, async ct =>
        {
            if (command.ParentId != location.ParentId)
            {
                await LocationRules.EnsureParentActiveAsync(db, command.ParentId, ct);
                await LocationRules.EnsureNoCycleAsync(db, location.Id, command.ParentId, ct);
            }

            await LocationRules.EnsureNameFreeAsync(db, command.ParentId, command.Name, location.Id, ct);

            var before = new { location.Name, location.ParentId };
            location.Update(command.Name, command.ParentId);
            audit.Record(AuditActions.LocationUpdated, AuditEntityTypes.Location, location.Id, before, new { location.Name, location.ParentId });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.LocationNameTaken, LocationRules.NameTakenMessage, ct);
        }, cancellationToken);

        return LocationItem.From(location);
    }
}
