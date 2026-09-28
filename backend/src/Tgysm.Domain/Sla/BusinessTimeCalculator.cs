using Tgysm.Domain.Common;

namespace Tgysm.Domain.Sla;

/// <summary>
/// Business-time accumulation over a <see cref="BusinessCalendarRules"/> (ADR-0018, spec §5.2): converts each UTC
/// boundary to local wall-clock time per day, clips to the day's working range, and skips holidays and closed days.
/// DST is handled by converting each local boundary back to UTC rather than doing arithmetic in local time, so a
/// working range that spans a spring-forward gap naturally loses the skipped hour.
/// </summary>
public static class BusinessTimeCalculator
{
    // Guards the day-by-day scan against an unbounded loop; far beyond any realistic SLA horizon.
    private const int MaxDaysScanned = 3650;

    /// <summary>Business seconds elapsed between <paramref name="fromUtc"/> and <paramref name="toUtc"/>; 0 when <paramref name="fromUtc"/> is not before <paramref name="toUtc"/>.</summary>
    public static long SecondsBetween(BusinessCalendarRules rules, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        EnsureHasWorkingDay(rules);
        if (fromUtc >= toUtc)
        {
            return 0;
        }

        var date = LocalDate(rules.Zone, fromUtc);
        var endDate = LocalDate(rules.Zone, toUtc);
        long total = 0;

        // No day-count guard here: the loop is already bounded by endDate (the caller's own range), however
        // long that range is — unlike AddSeconds below, this can never scan forever.
        for (; date <= endDate; date = date.AddDays(1))
        {
            if (!TryGetWorkingWindowUtc(rules, date, out var windowStart, out var windowEnd))
            {
                continue;
            }

            var overlapStart = windowStart > fromUtc ? windowStart : fromUtc;
            var overlapEnd = windowEnd < toUtc ? windowEnd : toUtc;
            if (overlapEnd > overlapStart)
            {
                total += (long)(overlapEnd - overlapStart).TotalSeconds;
            }
        }

        return total;
    }

    /// <summary>Moves <paramref name="fromUtc"/> forward by <paramref name="seconds"/> of business time, skipping non-working time.</summary>
    public static DateTimeOffset AddSeconds(BusinessCalendarRules rules, DateTimeOffset fromUtc, long seconds)
    {
        EnsureHasWorkingDay(rules);
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);

        var date = LocalDate(rules.Zone, fromUtc);
        var cursor = fromUtc;
        var remaining = seconds;

        for (var scanned = 0; ; date = date.AddDays(1))
        {
            if (++scanned > MaxDaysScanned)
            {
                throw new DomainException(ErrorCodes.SlaCalendarEmpty, "The business calendar produced no working time in a bounded scan.");
            }

            if (!TryGetWorkingWindowUtc(rules, date, out var windowStart, out var windowEnd))
            {
                continue;
            }

            var segmentStart = cursor > windowStart ? cursor : windowStart;
            if (segmentStart >= windowEnd)
            {
                continue;
            }

            var available = (long)(windowEnd - segmentStart).TotalSeconds;
            if (remaining <= available)
            {
                // Always return UTC: segmentStart can still carry the caller's original offset (e.g. +03:00)
                // when it is returned unmodified on the very first working day, and Npgsql's timestamptz column
                // (Task 11) rejects a non-zero offset.
                return segmentStart.AddSeconds(remaining).ToUniversalTime();
            }

            remaining -= available;
            cursor = windowEnd;
        }
    }

    private static bool TryGetWorkingWindowUtc(BusinessCalendarRules rules, DateOnly date, out DateTimeOffset windowStart, out DateTimeOffset windowEnd)
    {
        windowStart = default;
        windowEnd = default;
        if (rules.Holidays.Contains(date) || !rules.Hours.TryGetValue(date.DayOfWeek, out var range))
        {
            return false;
        }

        windowStart = ToUtc(rules.Zone, date, range.Start);
        windowEnd = ToUtc(rules.Zone, date, range.End);
        return true;
    }

    private static DateOnly LocalDate(TimeZoneInfo zone, DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).Date);

    private static DateTimeOffset ToUtc(TimeZoneInfo zone, DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        if (zone.IsInvalidTime(local))
        {
            // The wall-clock instant was skipped by a spring-forward transition; the first moment that actually
            // occurred is the first valid instant after the gap.
            do
            {
                local = local.AddMinutes(1);
            }
            while (zone.IsInvalidTime(local));
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    private static void EnsureHasWorkingDay(BusinessCalendarRules rules)
    {
        if (rules.Hours.Count == 0)
        {
            throw new DomainException(ErrorCodes.SlaCalendarEmpty, "The business calendar has no working day.");
        }
    }
}
