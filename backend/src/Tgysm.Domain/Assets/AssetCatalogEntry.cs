namespace Tgysm.Domain.Assets;

/// <summary>The catalog model an asset is created or edited with: its type and the brand and model names it shows.</summary>
public sealed record AssetCatalogEntry(Guid ModelId, AssetType Type, string BrandName, string ModelName);
