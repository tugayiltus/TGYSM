using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;

namespace Tgysm.Application.Features.Organization.Locations;

internal static class LocationRules
{
    public const string NameTakenMessage = "Another location with this name exists under the same parent.";

    public static IRuleBuilderOptions<T, string> ValidLocationName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(Location.NameMaxLength);

    public static async Task EnsureParentActiveAsync(IAppDbContext db, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is { } id && !await db.Locations.AnyAsync(l => l.Id == id && l.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "Parent location is missing or inactive.");
        }
    }

    public static async Task EnsureNameFreeAsync(IAppDbContext db, Guid? parentId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.Locations.AnyAsync(l => l.ParentId == parentId && l.NormalizedName == key && l.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.LocationNameTaken, NameTakenMessage);
        }
    }

    /// <summary>Walks up from the new parent; reaching <paramref name="locationId"/> means the move creates a cycle.</summary>
    public static async Task EnsureNoCycleAsync(IAppDbContext db, Guid locationId, Guid? newParentId, CancellationToken cancellationToken)
    {
        if (newParentId is null)
        {
            return;
        }

        var parents = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.ParentId, cancellationToken);
        var current = newParentId;
        for (var steps = 0; current is { } id && steps <= parents.Count; steps++)
        {
            if (id == locationId)
            {
                throw new DomainException(ErrorCodes.LocationCycle, "A location cannot be moved under itself or its descendant.");
            }

            current = parents.GetValueOrDefault(id);
        }
    }
}
