namespace Tgysm.Application.Reports;

/// <summary>An age band in whole days; <c>ToDays</c> null means open-ended.</summary>
public sealed record AgeBand(string Key, int FromDays, int? ToDays);

public static class ReportBuckets
{
    /// <summary>Ageing buckets of open tickets (spec §7.1): 0–1, 2–3, 4–7, 8+ days.</summary>
    public static readonly IReadOnlyList<AgeBand> AgeingDays =
    [
        new("0-1", 0, 1),
        new("2-3", 2, 3),
        new("4-7", 4, 7),
        new("8+", 8, null),
    ];

    public static AgeBand AgeingBand(int ageDays) =>
        AgeingDays.Last(band => ageDays >= band.FromDays);
}
