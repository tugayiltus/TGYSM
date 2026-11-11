using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;

namespace Tgysm.Application.Features.Assets;

public sealed record GetAssetLabelsQuery(IReadOnlyList<Guid> Ids);

public sealed record AssetLabelItem(Guid Id, string AssetTag, string Brand, string Model, string? SerialNumber);

/// <summary>Label data and the company address the QR links point to; never the browser's own origin (ADR-0028).</summary>
public sealed record AssetLabelsDto(string PublicBaseUrl, IReadOnlyList<AssetLabelItem> Items);

internal sealed class GetAssetLabelsValidator : AbstractValidator<GetAssetLabelsQuery>
{
    public GetAssetLabelsValidator() =>
        RuleFor(q => q.Ids).NotNull().Must(ids => ids.Count <= GetAssetLabelsHandler.MaxLabelsPerRequest);
}

public sealed class GetAssetLabelsHandler(IAppDbContext db, IOptionsMonitor<AppOptions> options, IValidator<GetAssetLabelsQuery> validator)
{
    /// <summary>One print run; a page of the inventory list is far smaller.</summary>
    public const int MaxLabelsPerRequest = 200;

    public async Task<AssetLabelsDto> HandleAsync(GetAssetLabelsQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var found = await db.Assets.AsNoTracking()
            .Where(a => query.Ids.Contains(a.Id))
            .Select(a => new AssetLabelItem(a.Id, a.AssetTag, a.Brand, a.Model, a.SerialNumber))
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        // Labels print in the order asked for; unknown ids are skipped.
        var items = query.Ids.Distinct().Where(found.ContainsKey).Select(id => found[id]).ToList();
        return new AssetLabelsDto(options.CurrentValue.PublicBaseUrl.TrimEnd('/'), items);
    }
}
