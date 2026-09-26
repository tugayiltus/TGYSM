using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Locations;

public sealed record LocationItem(Guid Id, string Name, Guid? ParentId, bool IsActive, uint Version)
{
    public static LocationItem From(Location location) =>
        new(location.Id, location.Name, location.ParentId, location.IsActive, location.Version);
}
