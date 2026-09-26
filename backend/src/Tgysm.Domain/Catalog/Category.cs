using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Catalog;

public enum CategoryScope
{
    Incident,
    Request,
    Both,
}

/// <summary>Ticket category; at most two levels (the depth rule across rows is checked by the handler).</summary>
public sealed class Category : Entity
{
    public const int NameMaxLength = 150;

    private Category()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public CategoryScope Scope { get; private set; }
    public bool IsActive { get; private set; }

    public static Category Create(string name, Guid? parentId, CategoryScope scope)
    {
        var category = new Category { IsActive = true };
        category.Update(name, parentId, scope);
        return category;
    }

    public void Update(string name, Guid? parentId, CategoryScope scope)
    {
        if (parentId == Id)
        {
            throw new DomainException(ErrorCodes.CategoryTooDeep, "A category cannot be its own parent.");
        }

        Name = name.Trim();
        NormalizedName = TurkishText.ToSearchKey(name);
        ParentId = parentId;
        Scope = scope;
    }

    public bool AppliesTo(TicketKind kind) =>
        Scope == CategoryScope.Both || (Scope == CategoryScope.Incident) == (kind == TicketKind.Incident);

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
