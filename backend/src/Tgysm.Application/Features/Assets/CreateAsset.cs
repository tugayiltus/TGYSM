using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Assets;

public sealed record CreateAssetCommand(
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

internal sealed class CreateAssetValidator : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetValidator() =>
        this.CardRules(c => c.AssetTag, c => c.Brand, c => c.Model, c => c.SerialNumber, c => c.Notes, c => c.Type, c => c.LocationId);
}

public sealed class CreateAssetHandler(
    IAppDbContext db,
    IValidator<CreateAssetCommand> validator,
    OrganizationReferences references,
    ICurrentActor actor,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<AssetItem> HandleAsync(CreateAssetCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        await AssetRules.EnsureUniqueAsync(db, command.AssetTag, command.SerialNumber, exceptId: null, cancellationToken);
        await references.EnsureActiveAsync(command.DepartmentId, command.LocationId, cancellationToken);

        var (asset, movement) = Asset.Create(
            command.AssetTag, command.Type, command.Brand, command.Model, command.SerialNumber, command.LocationId,
            command.DepartmentId, command.WarrantyEndsOn, command.PurchasedOn, command.Notes, clock.GetUtcNow(), actorUserId: actorId);

        db.Assets.Add(asset);
        db.AssetMovements.Add(movement);
        audit.Record(AuditActions.AssetCreated, AuditEntityTypes.Asset, asset.Id, newValues: new { asset.AssetTag, asset.Type, asset.Status });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.AssetTagTaken, AssetRules.TagTakenMessage, cancellationToken);
        return await AssetQueries.LoadItemAsync(db, asset.Id, cancellationToken);
    }
}
