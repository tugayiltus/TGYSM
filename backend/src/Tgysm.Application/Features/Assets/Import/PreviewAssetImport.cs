using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Organization;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Assets.Import;

public sealed record PreviewAssetImportCommand(string FileName, long Length, Stream Content);

/// <summary>Parses and checks a CSV file and stores the result as a previewed job. Nothing is written to the inventory.</summary>
public sealed class PreviewAssetImportHandler(IAppDbContext db, ICurrentActor actor, TimeProvider clock)
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxDataRows = 5_000;
    public const string FileField = "file";
    private const int HeaderRow = 1;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy"];
    private static readonly AssetStatus[] ImportableStatuses = [AssetStatus.InStock, AssetStatus.Maintenance, AssetStatus.Retired];
    private static readonly Dictionary<string, AssetType> TypeNames = SnakeNames<AssetType>();
    private static readonly Dictionary<string, AssetStatus> StatusNames = SnakeNames<AssetStatus>();

    public async Task<AssetImportJobItem> HandleAsync(PreviewAssetImportCommand command, CancellationToken cancellationToken)
    {
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        if (command.Length > MaxFileBytes)
        {
            throw FileError(ErrorCodes.ImportFileTooLarge, "The file is larger than 10 MB.");
        }

        string text;
        try
        {
            // Strict UTF-8: a Windows-1254 export would otherwise turn Turkish letters into U+FFFD and be stored as such.
            using var reader = new StreamReader(command.Content, StrictUtf8, detectEncodingFromByteOrderMarks: true);
            text = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            throw FileError(ErrorCodes.ImportEncoding, "The file is not UTF-8.");
        }

        var records = AssetCsvReader.Read(text);
        var columns = records.Count == 0 ? null : MapHeader(records[0]);
        if (columns is null)
        {
            throw FileError(ErrorCodes.ImportInvalidHeader, "The header row does not match the template.");
        }

        var dataRows = records.Skip(1)
            .Select((cells, index) => (Row: index + HeaderRow + 1, Cells: cells))
            .Where(r => r.Cells.Exists(c => c.Length > 0))
            .ToList();
        if (dataRows.Count > MaxDataRows)
        {
            throw FileError(ErrorCodes.ImportTooManyRows, "The file has more than 5,000 rows.");
        }

        var context = await RowContext.LoadAsync(db, dataRows.Select(r => Cell(r.Cells, columns, AssetCsvTemplate.AssetTag)), dataRows.Select(r => Cell(r.Cells, columns, AssetCsvTemplate.SerialNumber)), cancellationToken);
        var rows = new List<AssetImportRow>();
        var errors = new List<AssetImportError>();
        foreach (var (row, cells) in dataRows)
        {
            var rowErrors = new List<AssetImportError>();
            var parsed = ParseRow(row, field => Cell(cells, columns, field), context, rowErrors);
            if (rowErrors.Count == 0 && parsed is not null)
            {
                rows.Add(parsed);
            }

            errors.AddRange(rowErrors);
        }

        var errorRows = errors.Select(e => e.Row).Distinct().Count();
        var job = AssetImportJob.Create(
            command.FileName, dataRows.Count, dataRows.Count - errorRows, errorRows,
            JsonSerializer.Serialize(rows, AssetImportJson.Options), JsonSerializer.Serialize(errors, AssetImportJson.Options), actorId, clock.GetUtcNow());
        db.AssetImportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return AssetImportJson.ToItem(job);
    }

    private static AssetImportRow? ParseRow(int row, Func<string, string> cell, RowContext context, List<AssetImportError> errors)
    {
        void Error(string field, string code) => errors.Add(new AssetImportError(row, field, code));

        string? Text(string field, int maxLength, bool required)
        {
            var value = cell(field);
            if (value.Length == 0)
            {
                if (required)
                {
                    Error(field, ErrorCodes.Required);
                }

                return null;
            }

            if (value.Length > maxLength)
            {
                Error(field, ErrorCodes.TooLong);
                return null;
            }

            return value;
        }

        DateOnly? Date(string field)
        {
            var value = cell(field);
            if (value.Length == 0)
            {
                return null;
            }

            if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            Error(field, ErrorCodes.InvalidValue);
            return null;
        }

        // Optional: an empty tag is generated at commit (the preview consumes no numbers); a filled one is a legacy tag.
        var tag = Text(AssetCsvTemplate.AssetTag, Asset.TagMaxLength, required: false);
        var tagRejected = false;
        var brand = Text(AssetCsvTemplate.Brand, CatalogName.MaxLength, required: true);
        var model = Text(AssetCsvTemplate.Model, CatalogName.MaxLength, required: true);
        var serial = Text(AssetCsvTemplate.SerialNumber, Asset.SerialMaxLength, required: false);
        var notes = Text(AssetCsvTemplate.Notes, Asset.NotesMaxLength, required: false);
        var warranty = Date(AssetCsvTemplate.WarrantyEndsOn);
        var purchased = Date(AssetCsvTemplate.PurchasedOn);
        var type = ParseType(cell(AssetCsvTemplate.Type), Error);
        var status = ParseStatus(cell(AssetCsvTemplate.Status), Error);
        var location = ParseLocation(cell(AssetCsvTemplate.Location), context, Error);
        var department = ParseDepartment(cell(AssetCsvTemplate.Department), context, Error);

        if (tag is not null && AssetTagFormat.IsReserved(tag))
        {
            // DMR- numbers come from the sequence; an imported one would collide with a later generated tag.
            Error(AssetCsvTemplate.AssetTag, ErrorCodes.ImportReservedTag);
            tagRejected = true;
        }
        else if (tag is not null)
        {
            context.CheckTag(tag, Error);
        }

        // Brand and model must already be in the catalog (ADR-0023): no free text, so no spelling variants.
        AssetCatalogEntry? entry = null;
        if (brand is not null && model is not null && type is not null)
        {
            entry = context.FindModel(brand, type.Value, model);
            if (entry is null)
            {
                Error(AssetCsvTemplate.Model, ErrorCodes.ImportCatalogMissing);
            }
        }

        if (serial is not null)
        {
            context.CheckSerial(serial, Error);
        }

        return tagRejected || entry is null || status is null || location is null
            ? null
            : new AssetImportRow(row, tag, entry.Type, entry.ModelId, entry.BrandName, entry.ModelName, serial, location.Id, location.Path, department?.Id, department?.Name, status.Value, warranty, purchased, notes);
    }

    private static AssetType? ParseType(string value, Action<string, string> error)
    {
        if (value.Length == 0)
        {
            error(AssetCsvTemplate.Type, ErrorCodes.Required);
            return null;
        }

        if (TypeNames.TryGetValue(value, out var type))
        {
            return type;
        }

        error(AssetCsvTemplate.Type, ErrorCodes.InvalidValue);
        return null;
    }

    private static AssetStatus? ParseStatus(string value, Action<string, string> error)
    {
        if (value.Length == 0)
        {
            return AssetStatus.InStock;
        }

        if (!StatusNames.TryGetValue(value, out var status))
        {
            error(AssetCsvTemplate.Status, ErrorCodes.InvalidValue);
            return null;
        }

        if (!ImportableStatuses.Contains(status))
        {
            error(AssetCsvTemplate.Status, ErrorCodes.ImportStatusNotAllowed);
            return null;
        }

        return status;
    }

    private static LocationPaths.Entry? ParseLocation(string value, RowContext context, Action<string, string> error)
    {
        if (value.Length == 0)
        {
            error(AssetCsvTemplate.Location, ErrorCodes.Required);
            return null;
        }

        var match = context.Locations.Find(value);
        if (match.ErrorCode is not null)
        {
            error(AssetCsvTemplate.Location, match.ErrorCode);
        }

        return match.Location;
    }

    private static (Guid Id, string Name)? ParseDepartment(string value, RowContext context, Action<string, string> error)
    {
        if (value.Length == 0)
        {
            return null;
        }

        if (!context.Departments.TryGetValue(TurkishText.ToSearchKey(value), out var department))
        {
            error(AssetCsvTemplate.Department, ErrorCodes.ImportDepartmentNotFound);
            return null;
        }

        if (!department.IsActive)
        {
            error(AssetCsvTemplate.Department, ErrorCodes.ReferenceInactive);
            return null;
        }

        return (department.Id, department.Name);
    }

    private static Dictionary<string, int>? MapHeader(List<string> header)
    {
        var byKey = AssetCsvTemplate.Columns.ToDictionary(c => TurkishText.ToSearchKey(c.Header), c => c.Field);
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.Count; i++)
        {
            var key = TurkishText.ToSearchKey(header[i].TrimStart('﻿'));
            if (byKey.TryGetValue(key, out var field) && !positions.TryAdd(field, i))
            {
                return null;
            }
        }

        return AssetCsvTemplate.Columns.Where(c => c.Required).All(c => positions.ContainsKey(c.Field)) ? positions : null;
    }

    private static string Cell(List<string> cells, Dictionary<string, int> columns, string field) =>
        columns.TryGetValue(field, out var index) && index < cells.Count ? cells[index] : string.Empty;

    private static ValidationException FileError(string code, string message) =>
        new([new ValidationFailure(FileField, message) { ErrorCode = code }]);

    private static Dictionary<string, TEnum> SnakeNames<TEnum>()
        where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().ToDictionary(v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()), StringComparer.OrdinalIgnoreCase);

    /// <summary>Reference data and duplicate tracking shared by every row of one file.</summary>
    private sealed class RowContext
    {
        private readonly HashSet<string> _takenTags;
        private readonly HashSet<string> _takenSerials;
        private readonly HashSet<string> _fileTags = [];
        private readonly HashSet<string> _fileSerials = [];

        private readonly Dictionary<(string Brand, AssetType Type, string Model), AssetCatalogEntry> _models;

        private RowContext(
            LocationPaths locations,
            Dictionary<string, (Guid Id, string Name, bool IsActive)> departments,
            HashSet<string> takenTags,
            HashSet<string> takenSerials,
            Dictionary<(string Brand, AssetType Type, string Model), AssetCatalogEntry> models)
        {
            _models = models;
            Locations = locations;
            Departments = departments;
            _takenTags = takenTags;
            _takenSerials = takenSerials;
        }

        public LocationPaths Locations { get; }

        public Dictionary<string, (Guid Id, string Name, bool IsActive)> Departments { get; }

        public static async Task<RowContext> LoadAsync(IAppDbContext db, IEnumerable<string> tags, IEnumerable<string> serials, CancellationToken cancellationToken)
        {
            var normalizedTags = tags.Where(t => t.Length > 0).Select(Asset.NormalizeTag).Distinct().ToList();
            var normalizedSerials = serials.Select(Asset.NormalizeSerial).OfType<string>().Distinct().ToList();
            var takenTags = await db.Assets.AsNoTracking().Where(a => normalizedTags.Contains(a.NormalizedAssetTag)).Select(a => a.NormalizedAssetTag).ToListAsync(cancellationToken);
            var takenSerials = await db.Assets.AsNoTracking().Where(a => a.NormalizedSerialNumber != null && normalizedSerials.Contains(a.NormalizedSerialNumber)).Select(a => a.NormalizedSerialNumber!).ToListAsync(cancellationToken);
            var departments = await db.Departments.AsNoTracking().Select(d => new { d.Id, d.Name, d.NormalizedName, d.IsActive }).ToListAsync(cancellationToken);
            var models = await (from m in db.AssetModels.AsNoTracking()
                                join b in db.Brands.AsNoTracking() on m.BrandId equals b.Id
                                where m.IsActive && b.IsActive
                                select new { BrandKey = b.NormalizedName, ModelKey = m.NormalizedName, Entry = new AssetCatalogEntry(m.Id, m.Type, b.Name, m.Name) }).ToListAsync(cancellationToken);
            return new RowContext(
                await LocationPaths.LoadAsync(db, cancellationToken),
                departments.ToDictionary(d => d.NormalizedName, d => (d.Id, d.Name, d.IsActive), StringComparer.Ordinal),
                [.. takenTags],
                [.. takenSerials],
                models.ToDictionary(m => (m.BrandKey, m.Entry.Type, m.ModelKey), m => m.Entry));
        }

        /// <summary>The active catalog model with this brand, type and model name (compared by <see cref="CatalogName"/> key).</summary>
        public AssetCatalogEntry? FindModel(string brand, AssetType type, string model) =>
            _models.GetValueOrDefault((CatalogName.Normalize(brand), type, CatalogName.Normalize(model)));

        public void CheckTag(string tag, Action<string, string> error)
        {
            var key = Asset.NormalizeTag(tag);
            if (_takenTags.Contains(key))
            {
                error(AssetCsvTemplate.AssetTag, ErrorCodes.AssetTagTaken);
            }
            else if (!_fileTags.Add(key))
            {
                error(AssetCsvTemplate.AssetTag, ErrorCodes.ImportDuplicateInFile);
            }
        }

        public void CheckSerial(string serial, Action<string, string> error)
        {
            if (Asset.NormalizeSerial(serial) is not { } key)
            {
                return;
            }

            if (_takenSerials.Contains(key))
            {
                error(AssetCsvTemplate.SerialNumber, ErrorCodes.AssetSerialTaken);
            }
            else if (!_fileSerials.Add(key))
            {
                error(AssetCsvTemplate.SerialNumber, ErrorCodes.ImportDuplicateInFile);
            }
        }
    }
}
