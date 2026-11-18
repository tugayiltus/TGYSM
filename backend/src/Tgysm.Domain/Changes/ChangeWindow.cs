using Tgysm.Domain.Common;

namespace Tgysm.Domain.Changes;

/// <summary>Rules for deciding when two change windows collide.</summary>
public static class ChangeWindow
{
    /// <summary>Only changes in these states hold a maintenance slot.</summary>
    public static readonly ChangeStatus[] ConflictStatuses = [ChangeStatus.PendingApproval, ChangeStatus.Scheduled, ChangeStatus.Implementing];

    /// <summary>Windows are half-open [start, end): one that ends when another starts does not overlap it.</summary>
    public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd) =>
        aStart < bEnd && bStart < aEnd;

    /// <summary>Same non-empty affected service after Turkish-aware normalization.</summary>
    public static bool SameService(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
        && string.Equals(TurkishText.ToSearchKey(a), TurkishText.ToSearchKey(b), StringComparison.Ordinal);
}
