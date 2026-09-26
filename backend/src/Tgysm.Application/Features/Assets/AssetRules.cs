using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Assets;

internal static class AssetRules
{
    public const string TagTakenMessage = "Another asset uses this asset tag.";
    public const string SerialTakenMessage = "Another asset uses this serial number.";

    public static void CardRules<T>(this AbstractValidator<T> validator, Func<T, string> tag, Func<T, string> brand, Func<T, string> model, Func<T, string?> serial, Func<T, string?> notes, Func<T, AssetType> type, Func<T, Guid> location)
    {
        validator.RuleFor(c => tag(c)).NotEmpty().MaximumLength(Asset.TagMaxLength).OverridePropertyName("assetTag");
        validator.RuleFor(c => brand(c)).NotEmpty().MaximumLength(Asset.BrandMaxLength).OverridePropertyName("brand");
        validator.RuleFor(c => model(c)).NotEmpty().MaximumLength(Asset.ModelMaxLength).OverridePropertyName("model");
        validator.RuleFor(c => serial(c)).MaximumLength(Asset.SerialMaxLength).OverridePropertyName("serialNumber");
        validator.RuleFor(c => notes(c)).MaximumLength(Asset.NotesMaxLength).OverridePropertyName("notes");
        validator.RuleFor(c => type(c)).IsInEnum().OverridePropertyName("type");
        validator.RuleFor(c => location(c)).NotEmpty().OverridePropertyName("locationId");
    }

    public static async Task EnsureUniqueAsync(IAppDbContext db, string tag, string? serial, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalizedTag = Asset.NormalizeTag(tag);
        if (await db.Assets.AnyAsync(a => a.NormalizedAssetTag == normalizedTag && a.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.AssetTagTaken, TagTakenMessage);
        }

        var normalizedSerial = Asset.NormalizeSerial(serial);
        if (normalizedSerial is not null
            && await db.Assets.AnyAsync(a => a.NormalizedSerialNumber == normalizedSerial && a.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.AssetSerialTaken, SerialTakenMessage);
        }
    }
}
