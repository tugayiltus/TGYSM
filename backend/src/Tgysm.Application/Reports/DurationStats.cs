using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Tgysm.Application.Reports;

public sealed record DurationSums(long Count, long CalendarSeconds, long SlaSeconds)
{
    public double? CalendarAverage => Count == 0 ? null : (double)CalendarSeconds / Count;

    public double? SlaAverage => Count == 0 ? null : (double)SlaSeconds / Count;
}

/// <summary>Duration aggregates computed in SQL, one round trip each.</summary>
public static class DurationStats
{
    /// <summary>Average of <paramref name="seconds"/> and how many there are; no values gives a null average.</summary>
    public static async Task<(double? Average, long Count)> AverageAsync(IQueryable<long> seconds, CancellationToken cancellationToken)
    {
        var stats = await seconds.GroupBy(_ => 1)
            .Select(g => new { Count = g.LongCount(), Average = g.Average(s => (double)s) })
            .SingleOrDefaultAsync(cancellationToken);
        return stats is null ? (null, 0) : (stats.Average, stats.Count);
    }

    /// <summary>Median of <paramref name="seconds"/> (the mean of the two middle values for an even count); no values gives null. Read in memory: a median has no portable SQL form.</summary>
    public static async Task<double?> MedianAsync(IQueryable<long> seconds, CancellationToken cancellationToken)
    {
        var sorted = await seconds.OrderBy(s => s).ToListAsync(cancellationToken);
        if (sorted.Count == 0)
        {
            return null;
        }

        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2d;
    }

    /// <summary>Count and the sums of the two per-row second expressions, as plain aggregates (EF cannot group a constructor projection).</summary>
    public static async Task<DurationSums> SumAsync<T>(
        IQueryable<T> rows, Expression<Func<T, long>> calendarSeconds, Expression<Func<T, long>> slaSeconds, CancellationToken cancellationToken)
    {
        var count = await rows.LongCountAsync(cancellationToken);
        if (count == 0)
        {
            return new DurationSums(0, 0, 0);
        }

        return new DurationSums(count, await rows.SumAsync(calendarSeconds, cancellationToken), await rows.SumAsync(slaSeconds, cancellationToken));
    }
}
