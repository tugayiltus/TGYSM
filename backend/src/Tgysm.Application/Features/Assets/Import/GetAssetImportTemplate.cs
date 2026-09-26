using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Organization;

namespace Tgysm.Application.Features.Assets.Import;

public sealed record AssetImportTemplate(string FileName, byte[] Content);

/// <summary>Builds the template; its example row uses a real active location so it can be imported as it is.</summary>
public sealed class GetAssetImportTemplateHandler(IAppDbContext db)
{
    public const string FileName = "cihaz-sablonu.csv";
    private const string PlaceholderLocation = "Kampüs / Bina";

    public async Task<AssetImportTemplate> HandleAsync(CancellationToken cancellationToken)
    {
        var paths = await LocationPaths.LoadAsync(db, cancellationToken);
        var example = paths.Entries.Where(e => e.IsActive).OrderBy(e => e.Path, StringComparer.Ordinal).Select(e => e.Path).FirstOrDefault();
        return new AssetImportTemplate(FileName, AssetCsvTemplate.Build(example ?? PlaceholderLocation));
    }
}
