using Tgysm.Domain.Common;

namespace Tgysm.Domain.Assets;

/// <summary>
/// A previewed CSV file: the parsed valid rows and per-row errors, stored as JSON so the commit inserts exactly what
/// the user saw. Committing is idempotent: a committed job keeps its result and inserts nothing again.
/// </summary>
public sealed class AssetImportJob : Entity
{
    public const int FileNameMaxLength = 255;

    private AssetImportJob()
    {
    }

    public string FileName { get; private set; } = string.Empty;
    public AssetImportStatus Status { get; private set; }
    public int TotalRows { get; private set; }
    public int ValidRows { get; private set; }
    public int ErrorRows { get; private set; }

    /// <summary>Parsed valid rows (JSON array).</summary>
    public string RowsJson { get; private set; } = string.Empty;

    /// <summary>Per-row errors (JSON array).</summary>
    public string ErrorsJson { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }

    public static AssetImportJob Create(
        string fileName, int totalRows, int validRows, int errorRows, string rowsJson, string errorsJson, Guid createdByUserId, DateTimeOffset now)
    {
        var name = fileName.Trim();
        return new AssetImportJob
        {
            FileName = name.Length > FileNameMaxLength ? name[..FileNameMaxLength] : name,
            Status = AssetImportStatus.Previewed,
            TotalRows = totalRows,
            ValidRows = validRows,
            ErrorRows = errorRows,
            RowsJson = rowsJson,
            ErrorsJson = errorsJson,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
        };
    }

    /// <summary>Marks the job committed. Returns false when it already was, so the caller inserts nothing.</summary>
    public bool Commit(DateTimeOffset now)
    {
        if (ErrorRows > 0)
        {
            throw new DomainException(ErrorCodes.ImportHasErrors, "Fix the rows with errors before committing the import.");
        }

        if (Status == AssetImportStatus.Committed)
        {
            return false;
        }

        Status = AssetImportStatus.Committed;
        CommittedAt = now;
        return true;
    }
}
