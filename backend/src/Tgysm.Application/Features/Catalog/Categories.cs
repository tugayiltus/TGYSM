using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Catalog;

public sealed record CategoryItem(Guid Id, string Name, Guid? ParentId, CategoryScope Scope, bool IsActive, uint Version)
{
    public static CategoryItem From(Category c) => new(c.Id, c.Name, c.ParentId, c.Scope, c.IsActive, c.Version);
}

public sealed record CreateCategoryCommand(string Name, Guid? ParentId, CategoryScope Scope);

public sealed record UpdateCategoryCommand(Guid Id, uint Version, string Name, Guid? ParentId, CategoryScope Scope);

public sealed record SetCategoryActiveCommand(Guid Id, uint Version, bool IsActive);

internal sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Category.NameMaxLength);
        RuleFor(c => c.Scope).IsInEnum();
    }
}

internal sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Category.NameMaxLength);
        RuleFor(c => c.Scope).IsInEnum();
    }
}

internal static class CategoryRules
{
    public const string NameTakenMessage = "Another category with this name exists under the same parent.";
    private const string TooDeepMessage = "Categories have at most two levels.";

    /// <summary>The parent must be an active top-level category, and a category with children stays top-level.</summary>
    public static async Task EnsurePlacementAsync(IAppDbContext db, Guid? categoryId, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is not { } id)
        {
            return;
        }

        var parent = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new DomainException(ErrorCodes.ReferenceInactive, "Parent category is missing.");
        if (!parent.IsActive)
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "Parent category is inactive.");
        }

        if (parent.ParentId is not null || parent.Id == categoryId
            || (categoryId is { } self && await db.Categories.AnyAsync(c => c.ParentId == self, cancellationToken)))
        {
            throw new DomainException(ErrorCodes.CategoryTooDeep, TooDeepMessage);
        }
    }

    public static async Task EnsureNameFreeAsync(IAppDbContext db, Guid? parentId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.Categories.AnyAsync(c => c.ParentId == parentId && c.NormalizedName == key && c.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.CategoryNameTaken, NameTakenMessage);
        }
    }
}

public sealed class ListCategoriesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<CategoryItem>> HandleAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.NormalizedName)
            .Select(c => new CategoryItem(c.Id, c.Name, c.ParentId, c.Scope, c.IsActive, c.Version))
            .ToListAsync(cancellationToken);
}

public sealed class CreateCategoryHandler(IAppDbContext db, IValidator<CreateCategoryCommand> validator, IAuditLog audit)
{
    public async Task<CategoryItem> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await CategoryRules.EnsurePlacementAsync(db, null, command.ParentId, cancellationToken);
        await CategoryRules.EnsureNameFreeAsync(db, command.ParentId, command.Name, null, cancellationToken);

        var category = Category.Create(command.Name, command.ParentId, command.Scope);
        db.Categories.Add(category);
        audit.Record(AuditActions.CategoryCreated, AuditEntityTypes.Category, category.Id, newValues: new { category.Name, category.ParentId, category.Scope });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.CategoryNameTaken, CategoryRules.NameTakenMessage, cancellationToken);
        return CategoryItem.From(category);
    }
}

public sealed class UpdateCategoryHandler(IAppDbContext db, IValidator<UpdateCategoryCommand> validator, IAuditLog audit)
{
    public async Task<CategoryItem> HandleAsync(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Category);
        db.SetExpectedVersion(category, command.Version);
        await CategoryRules.EnsurePlacementAsync(db, category.Id, command.ParentId, cancellationToken);
        await CategoryRules.EnsureNameFreeAsync(db, command.ParentId, command.Name, category.Id, cancellationToken);

        var before = new { category.Name, category.ParentId, category.Scope };
        category.Update(command.Name, command.ParentId, command.Scope);
        audit.Record(AuditActions.CategoryUpdated, AuditEntityTypes.Category, category.Id, before, new { category.Name, category.ParentId, category.Scope });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.CategoryNameTaken, CategoryRules.NameTakenMessage, cancellationToken);
        return CategoryItem.From(category);
    }
}

public sealed class SetCategoryActiveHandler(IAppDbContext db, IAuditLog audit)
{
    public async Task<CategoryItem> HandleAsync(SetCategoryActiveCommand command, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Category);
        if (category.IsActive == command.IsActive)
        {
            return CategoryItem.From(category);
        }

        db.SetExpectedVersion(category, command.Version);
        if (command.IsActive)
        {
            category.Reactivate();
        }
        else
        {
            category.Deactivate();
        }

        audit.Record(command.IsActive ? AuditActions.CategoryReactivated : AuditActions.CategoryDeactivated, AuditEntityTypes.Category, category.Id);
        await db.SaveChangesAsync(cancellationToken);
        return CategoryItem.From(category);
    }
}
