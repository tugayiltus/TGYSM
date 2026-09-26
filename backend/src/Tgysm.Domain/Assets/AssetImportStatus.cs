namespace Tgysm.Domain.Assets;

/// <summary>
/// State of a CSV import job. A commit is all-or-nothing: a failed commit rolls back and the job stays
/// <see cref="Previewed"/>, so there is no separate failed state.
/// </summary>
public enum AssetImportStatus
{
    Previewed,
    Committed,
}
