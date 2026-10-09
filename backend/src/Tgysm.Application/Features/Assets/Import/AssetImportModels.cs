using System.Text.Json;
using System.Text.Json.Serialization;
using Tgysm.Domain.Assets;

namespace Tgysm.Application.Features.Assets.Import;

/// <summary>A row that passed every check; the commit inserts exactly these values.</summary>
public sealed record AssetImportRow(
    int Row,
    string? AssetTag,
    AssetType Type,
    Guid ModelId,
    string Brand,
    string Model,
    string? SerialNumber,
    Guid LocationId,
    string LocationPath,
    Guid? DepartmentId,
    string? DepartmentName,
    AssetStatus Status,
    DateOnly? WarrantyEndsOn,
    DateOnly? PurchasedOn,
    string? Notes);

/// <summary>One problem in one cell. <see cref="Row"/> is the spreadsheet row (the header is row 1).</summary>
public sealed record AssetImportError(int Row, string Field, string Code);

public sealed record AssetImportJobItem(
    Guid JobId,
    string FileName,
    AssetImportStatus Status,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    IReadOnlyList<AssetImportRow> Rows,
    IReadOnlyList<AssetImportError> Errors,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CommittedAt);

internal static class AssetImportJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static AssetImportJobItem ToItem(AssetImportJob job) => new(
        job.Id,
        job.FileName,
        job.Status,
        job.TotalRows,
        job.ValidRows,
        job.ErrorRows,
        JsonSerializer.Deserialize<List<AssetImportRow>>(job.RowsJson, Options) ?? [],
        JsonSerializer.Deserialize<List<AssetImportError>>(job.ErrorsJson, Options) ?? [],
        job.CreatedAt,
        job.CommittedAt);
}
