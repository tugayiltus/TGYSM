using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Assets.Catalog;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Assets;

public sealed record UpdateAssetCommand(
    Guid Id,
    uint Version,
    Guid ModelId,
    string? SerialNumber,
    Guid LocationId,
    Guid? DepartmentId,
    DateOnly? WarrantyEndsOn,
    DateOnly? PurchasedOn,
    string? Notes);

internal sealed class UpdateAssetValidator : AbstractValidator<UpdateAssetCommand>
{
    public UpdateAssetValidator() =>
        this.CardRules(c => c.ModelId, c => c.SerialNumber, c => c.Notes, c => c.LocationId);
}

public sealed class UpdateAssetHandler(
    IAppDbContext db,
    IValidator<UpdateAssetCommand> validator,
    OrganizationReferences references,
    ICurrentActor actor,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<AssetItem> HandleAsync(UpdateAssetCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Asset);
        db.SetExpectedVersion(asset, command.Version);

        await AssetRules.EnsureUniqueAsync(db, asset.AssetTag, command.SerialNumber, asset.Id, cancellationToken);
        await references.EnsureActiveAsync(
            command.DepartmentId == asset.DepartmentId ? null : command.DepartmentId,
            command.LocationId == asset.LocationId ? null : command.LocationId,
            cancellationToken);

        // Under the catalog lock, so the brand/model names written here cannot be overtaken by a concurrent rename.
        await db.RunSerializedAsync(SerializationLocks.AssetCatalog, async ct =>
        {
            // A model deactivated after this asset was registered stays valid for the asset itself.
            var model = await AssetModelResolver.ResolveAsync(db, command.ModelId, keepInactiveId: asset.ModelId, ct);
            var before = new { asset.AssetTag, asset.Type, asset.Brand, asset.Model, asset.SerialNumber, asset.LocationId, asset.DepartmentId };
            var movement = asset.Update(
                model, command.SerialNumber, command.LocationId,
                command.DepartmentId, command.WarrantyEndsOn, command.PurchasedOn, command.Notes, actorId, clock.GetUtcNow());
            db.AssetMovements.Add(movement);
            audit.Record(AuditActions.AssetUpdated, AuditEntityTypes.Asset, asset.Id, before,
                new { asset.AssetTag, asset.Type, asset.Brand, asset.Model, asset.SerialNumber, asset.LocationId, asset.DepartmentId });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.AssetTagTaken, AssetRules.TagTakenMessage, ct);
        }, cancellationToken);
        return await AssetQueries.LoadItemAsync(db, asset.Id, cancellationToken);
    }
}
