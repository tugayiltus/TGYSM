using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Assets.Catalog;

public sealed record BrandItem(Guid Id, string Name, bool IsActive, uint Version);

public sealed record AssetModelItem(Guid Id, Guid BrandId, string BrandName, AssetType Type, string Name, bool IsActive, uint Version);

public sealed record CatalogModel(Guid Id, AssetType Type, string Name);

/// <summary>An active brand with its active models, for asset forms.</summary>
public sealed record CatalogBrand(Guid Id, string Name, IReadOnlyList<CatalogModel> Models);

public sealed record CreateBrandCommand(string Name);

public sealed record RenameBrandCommand(Guid Id, uint Version, string Name);

public sealed record SetBrandActiveCommand(Guid Id, uint Version, bool IsActive);

public sealed record CreateAssetModelCommand(Guid BrandId, AssetType Type, string Name);

public sealed record RenameAssetModelCommand(Guid Id, uint Version, string Name);

public sealed record SetAssetModelActiveCommand(Guid Id, uint Version, bool IsActive);

internal sealed class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(CatalogName.MaxLength);
}

internal sealed class RenameBrandValidator : AbstractValidator<RenameBrandCommand>
{
    public RenameBrandValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(CatalogName.MaxLength);
}

internal sealed class CreateAssetModelValidator : AbstractValidator<CreateAssetModelCommand>
{
    public CreateAssetModelValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(CatalogName.MaxLength);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.BrandId).NotEmpty();
    }
}

internal sealed class RenameAssetModelValidator : AbstractValidator<RenameAssetModelCommand>
{
    public RenameAssetModelValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(CatalogName.MaxLength);
}

internal static class CatalogRules
{
    public const string DuplicateMessage = "The catalog already has this name.";

    public static DomainException Duplicate() => new(ErrorCodes.AssetCatalogDuplicate, DuplicateMessage);

    public static async Task EnsureBrandNameFreeAsync(IAppDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = CatalogName.Normalize(name);
        if (await db.Brands.AnyAsync(b => b.NormalizedName == key && b.Id != exceptId, cancellationToken))
        {
            throw Duplicate();
        }
    }

    public static async Task EnsureModelNameFreeAsync(IAppDbContext db, Guid brandId, AssetType type, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = CatalogName.Normalize(name);
        if (await db.AssetModels.AnyAsync(m => m.BrandId == brandId && m.Type == type && m.NormalizedName == key && m.Id != exceptId, cancellationToken))
        {
            throw Duplicate();
        }
    }

    public static async Task<BrandItem> LoadBrandAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        await db.Brands.AsNoTracking().Where(b => b.Id == id).Select(b => new BrandItem(b.Id, b.Name, b.IsActive, b.Version)).SingleAsync(cancellationToken);

    public static async Task<AssetModelItem> LoadModelAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        await (from m in db.AssetModels.AsNoTracking()
               join b in db.Brands.AsNoTracking() on m.BrandId equals b.Id
               where m.Id == id
               select new AssetModelItem(m.Id, m.BrandId, b.Name, m.Type, m.Name, m.IsActive, m.Version)).SingleAsync(cancellationToken);
}

public sealed class ListBrandsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<BrandItem>> HandleAsync(CancellationToken cancellationToken) =>
        await db.Brands.AsNoTracking().OrderBy(b => b.NormalizedName).Select(b => new BrandItem(b.Id, b.Name, b.IsActive, b.Version)).ToListAsync(cancellationToken);
}

public sealed class CreateBrandHandler(IAppDbContext db, IValidator<CreateBrandCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<BrandItem> HandleAsync(CreateBrandCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await CatalogRules.EnsureBrandNameFreeAsync(db, command.Name, null, cancellationToken);
        var brand = Brand.Create(command.Name, clock.GetUtcNow());
        db.Brands.Add(brand);
        audit.Record(AuditActions.BrandCreated, AuditEntityTypes.Brand, brand.Id, newValues: new { brand.Name });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.AssetCatalogDuplicate, CatalogRules.DuplicateMessage, cancellationToken);
        return new BrandItem(brand.Id, brand.Name, brand.IsActive, brand.Version);
    }
}

/// <summary>Renames a brand and the brand snapshot of its assets in one transaction.</summary>
public sealed class RenameBrandHandler(IAppDbContext db, IValidator<RenameBrandCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<BrandItem> HandleAsync(RenameBrandCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await db.RunSerializedAsync(SerializationLocks.AssetCatalog, async ct =>
        {
            var brand = await db.Brands.SingleOrDefaultAsync(b => b.Id == command.Id, ct) ?? throw new NotFoundException(AuditEntityTypes.Brand);
            db.SetExpectedVersion(brand, command.Version);
            await CatalogRules.EnsureBrandNameFreeAsync(db, command.Name, brand.Id, ct);
            var before = brand.Name;
            brand.Rename(command.Name, clock.GetUtcNow());
            audit.Record(AuditActions.BrandRenamed, AuditEntityTypes.Brand, brand.Id, new { Name = before }, new { brand.Name });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.AssetCatalogDuplicate, CatalogRules.DuplicateMessage, ct);
            var modelIds = db.AssetModels.Where(m => m.BrandId == brand.Id).Select(m => m.Id);
            await db.Assets.Where(a => modelIds.Contains(a.ModelId)).ExecuteUpdateAsync(set => set.SetProperty(a => a.Brand, brand.Name), ct);
        }, cancellationToken);
        return await CatalogRules.LoadBrandAsync(db, command.Id, cancellationToken);
    }
}

public sealed class SetBrandActiveHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock)
{
    public async Task<BrandItem> HandleAsync(SetBrandActiveCommand command, CancellationToken cancellationToken)
    {
        var brand = await db.Brands.SingleOrDefaultAsync(b => b.Id == command.Id, cancellationToken) ?? throw new NotFoundException(AuditEntityTypes.Brand);
        if (brand.IsActive != command.IsActive)
        {
            db.SetExpectedVersion(brand, command.Version);
            if (command.IsActive)
            {
                brand.Reactivate(clock.GetUtcNow());
            }
            else
            {
                brand.Deactivate(clock.GetUtcNow());
            }

            audit.Record(command.IsActive ? AuditActions.BrandReactivated : AuditActions.BrandDeactivated, AuditEntityTypes.Brand, brand.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new BrandItem(brand.Id, brand.Name, brand.IsActive, brand.Version);
    }
}

public sealed record ListAssetModelsQuery(Guid? BrandId, AssetType? Type);

public sealed class ListAssetModelsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<AssetModelItem>> HandleAsync(ListAssetModelsQuery query, CancellationToken cancellationToken) =>
        await (from m in db.AssetModels.AsNoTracking()
               join b in db.Brands.AsNoTracking() on m.BrandId equals b.Id
               where (query.BrandId == null || m.BrandId == query.BrandId) && (query.Type == null || m.Type == query.Type)
               orderby b.NormalizedName, m.Type, m.NormalizedName
               select new AssetModelItem(m.Id, m.BrandId, b.Name, m.Type, m.Name, m.IsActive, m.Version)).ToListAsync(cancellationToken);
}

public sealed class CreateAssetModelHandler(IAppDbContext db, IValidator<CreateAssetModelCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<AssetModelItem> HandleAsync(CreateAssetModelCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        if (!await db.Brands.AnyAsync(b => b.Id == command.BrandId && b.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "The brand does not exist or is inactive.");
        }

        await CatalogRules.EnsureModelNameFreeAsync(db, command.BrandId, command.Type, command.Name, null, cancellationToken);
        var model = AssetModel.Create(command.BrandId, command.Type, command.Name, clock.GetUtcNow());
        db.AssetModels.Add(model);
        audit.Record(AuditActions.AssetModelCreated, AuditEntityTypes.AssetModel, model.Id, newValues: new { model.BrandId, model.Type, model.Name });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.AssetCatalogDuplicate, CatalogRules.DuplicateMessage, cancellationToken);
        return await CatalogRules.LoadModelAsync(db, model.Id, cancellationToken);
    }
}

/// <summary>Renames a model and the model snapshot of its assets in one transaction.</summary>
public sealed class RenameAssetModelHandler(IAppDbContext db, IValidator<RenameAssetModelCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<AssetModelItem> HandleAsync(RenameAssetModelCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await db.RunSerializedAsync(SerializationLocks.AssetCatalog, async ct =>
        {
            var model = await db.AssetModels.SingleOrDefaultAsync(m => m.Id == command.Id, ct) ?? throw new NotFoundException(AuditEntityTypes.AssetModel);
            db.SetExpectedVersion(model, command.Version);
            await CatalogRules.EnsureModelNameFreeAsync(db, model.BrandId, model.Type, command.Name, model.Id, ct);
            var before = model.Name;
            model.Rename(command.Name, clock.GetUtcNow());
            audit.Record(AuditActions.AssetModelRenamed, AuditEntityTypes.AssetModel, model.Id, new { Name = before }, new { model.Name });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.AssetCatalogDuplicate, CatalogRules.DuplicateMessage, ct);
            await db.Assets.Where(a => a.ModelId == model.Id).ExecuteUpdateAsync(set => set.SetProperty(a => a.Model, model.Name), ct);
        }, cancellationToken);
        return await CatalogRules.LoadModelAsync(db, command.Id, cancellationToken);
    }
}

public sealed class SetAssetModelActiveHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock)
{
    public async Task<AssetModelItem> HandleAsync(SetAssetModelActiveCommand command, CancellationToken cancellationToken)
    {
        var model = await db.AssetModels.SingleOrDefaultAsync(m => m.Id == command.Id, cancellationToken) ?? throw new NotFoundException(AuditEntityTypes.AssetModel);
        if (model.IsActive != command.IsActive)
        {
            db.SetExpectedVersion(model, command.Version);
            if (command.IsActive)
            {
                model.Reactivate(clock.GetUtcNow());
            }
            else
            {
                model.Deactivate(clock.GetUtcNow());
            }

            audit.Record(command.IsActive ? AuditActions.AssetModelReactivated : AuditActions.AssetModelDeactivated, AuditEntityTypes.AssetModel, model.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await CatalogRules.LoadModelAsync(db, model.Id, cancellationToken);
    }
}

/// <summary>Active brands with their active models for asset forms (IT staff and admins).</summary>
public sealed class GetAssetCatalogHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<CatalogBrand>> HandleAsync(CancellationToken cancellationToken)
    {
        var rows = await (from m in db.AssetModels.AsNoTracking()
                          join b in db.Brands.AsNoTracking() on m.BrandId equals b.Id
                          where m.IsActive && b.IsActive
                          orderby b.NormalizedName, m.NormalizedName
                          select new { BrandId = b.Id, BrandName = b.Name, Model = new CatalogModel(m.Id, m.Type, m.Name) }).ToListAsync(cancellationToken);
        return [.. rows.GroupBy(r => (r.BrandId, r.BrandName)).Select(g => new CatalogBrand(g.Key.BrandId, g.Key.BrandName, [.. g.Select(r => r.Model)]))];
    }
}

/// <summary>Resolves the model an asset is created or edited with.</summary>
internal static class AssetModelResolver
{
    public const string InactiveMessage = "The model does not exist or is inactive.";

    /// <param name="keepInactiveId">The asset's current model: kept even if it was deactivated later.</param>
    public static async Task<AssetCatalogEntry> ResolveAsync(IAppDbContext db, Guid modelId, Guid? keepInactiveId, CancellationToken cancellationToken)
    {
        var entry = await (from m in db.AssetModels.AsNoTracking()
                           join b in db.Brands.AsNoTracking() on m.BrandId equals b.Id
                           where m.Id == modelId && ((m.IsActive && b.IsActive) || m.Id == keepInactiveId)
                           select new AssetCatalogEntry(m.Id, m.Type, b.Name, m.Name)).SingleOrDefaultAsync(cancellationToken);
        return entry ?? throw new DomainException(ErrorCodes.AssetModelInactive, InactiveMessage);
    }
}
