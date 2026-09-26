using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Assets;

public sealed record UpdateAssetCommand(
    Guid Id,
    uint Version,
    string AssetTag,
    AssetType Type,
    string Brand,
    string Model,
    string? SerialNumber,
    Guid LocationId,
    Guid? DepartmentId,
    DateOnly? WarrantyEndsOn,
    DateOnly? PurchasedOn,
    string? Notes);

internal sealed class UpdateAssetValidator : AbstractValidator<UpdateAssetCommand>
{
    public UpdateAssetValidator() =>
        this.CardRules(c => c.AssetTag, c => c.Brand, c => c.Model, c => c.SerialNumber, c => c.Notes, c => c.Type, c => c.LocationId);
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

        await AssetRules.EnsureUniqueAsync(db, command.AssetTag, command.SerialNumber, asset.Id, cancellationToken);
        await references.EnsureActiveAsync(
            command.DepartmentId == asset.DepartmentId ? null : command.DepartmentId,
            command.LocationId == asset.LocationId ? null : command.LocationId,
            cancellationToken);

        var before = new { asset.AssetTag, asset.Type, asset.Brand, asset.Model, asset.SerialNumber, asset.LocationId, asset.DepartmentId };
        var movement = asset.Update(
            command.AssetTag, command.Type, command.Brand, command.Model, command.SerialNumber, command.LocationId,
            command.DepartmentId, command.WarrantyEndsOn, command.PurchasedOn, command.Notes, actorId, clock.GetUtcNow());
        db.AssetMovements.Add(movement);
        audit.Record(AuditActions.AssetUpdated, AuditEntityTypes.Asset, asset.Id, before,
            new { asset.AssetTag, asset.Type, asset.Brand, asset.Model, asset.SerialNumber, asset.LocationId, asset.DepartmentId });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.AssetTagTaken, AssetRules.TagTakenMessage, cancellationToken);
        return await AssetQueries.LoadItemAsync(db, asset.Id, cancellationToken);
    }
}
