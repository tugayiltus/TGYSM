namespace Tgysm.Domain.Sla;

/// <summary>
/// The company's working calendar (ADR-0018): a time zone, one optional <see cref="WorkingRange"/> per weekday
/// (a weekday missing from <paramref name="Hours"/> is closed), and the set of holiday dates that are otherwise
/// working days. Calendar and holiday changes apply only to future accumulation (spec §5.1).
/// </summary>
public sealed record BusinessCalendarRules(
    TimeZoneInfo Zone,
    IReadOnlyDictionary<DayOfWeek, WorkingRange> Hours,
    IReadOnlySet<DateOnly> Holidays);
