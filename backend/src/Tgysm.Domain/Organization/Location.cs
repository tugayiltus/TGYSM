using Tgysm.Domain.Common;

namespace Tgysm.Domain.Organization;

/// <summary>Location tree node: campus → building/area → optional sub-location.</summary>
public sealed class Location : Entity
{
    public const int NameMaxLength = 150;

    private Location()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public bool IsActive { get; private set; }

    public static Location Create(string name, Guid? parentId)
    {
        var location = new Location { IsActive = true };
        location.Update(name, parentId);
        return location;
    }

    public void Update(string name, Guid? parentId)
    {
        if (parentId == Id)
        {
            throw new DomainException(ErrorCodes.LocationCycle, "A location cannot be its own parent.");
        }

        Name = name.Trim();
        NormalizedName = TurkishText.ToSearchKey(name);
        ParentId = parentId;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
