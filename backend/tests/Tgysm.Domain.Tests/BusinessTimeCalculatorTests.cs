using Tgysm.Domain.Common;
using Tgysm.Domain.Sla;

namespace Tgysm.Domain.Tests;

public sealed class BusinessTimeCalculatorTests
{
    // Fixed +03:00 all year round (Turkey abolished DST in 2016), so ordinary tests need no DST reasoning.
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly WorkingRange OfficeHours = new(new TimeOnly(9, 0), new TimeOnly(18, 0));

    // 2026-01-05 is a Monday; 2026-01-03/04 is the preceding Saturday/Sunday.
    private static readonly DateOnly Monday = new(2026, 1, 5);
    private static readonly DateOnly Friday = new(2026, 1, 9);
    private static readonly DateOnly Saturday = new(2026, 1, 10);
    private static readonly DateOnly NextMonday = new(2026, 1, 12);

    private static BusinessCalendarRules MondayToFriday(params DateOnly[] holidays) => new(
        Istanbul,
        new Dictionary<DayOfWeek, WorkingRange>
        {
            [DayOfWeek.Monday] = OfficeHours,
            [DayOfWeek.Tuesday] = OfficeHours,
            [DayOfWeek.Wednesday] = OfficeHours,
            [DayOfWeek.Thursday] = OfficeHours,
            [DayOfWeek.Friday] = OfficeHours,
        },
        holidays.ToHashSet());

    private static DateTimeOffset LocalIstanbul(DateOnly date, int hour, int minute = 0) =>
        new(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(3));

    [Fact]
    public void Working_range_start_must_be_before_its_end()
    {
        Assert.Throws<ArgumentException>(() => new WorkingRange(new TimeOnly(18, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void Counts_the_full_span_when_both_ends_are_inside_the_same_working_day()
    {
        var rules = MondayToFriday();

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, LocalIstanbul(Monday, 10), LocalIstanbul(Monday, 14));

        Assert.Equal(4 * 3600L, seconds);
    }

    [Fact]
    public void Clips_a_start_before_opening_to_the_opening_time()
    {
        var rules = MondayToFriday();

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, LocalIstanbul(Monday, 7), LocalIstanbul(Monday, 10));

        Assert.Equal(1 * 3600L, seconds);
    }

    [Fact]
    public void Clips_an_end_after_closing_to_the_closing_time()
    {
        var rules = MondayToFriday();

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, LocalIstanbul(Monday, 17), LocalIstanbul(Monday, 19));

        Assert.Equal(1 * 3600L, seconds);
    }

    [Fact]
    public void Skips_the_weekend_between_friday_and_the_next_monday()
    {
        var rules = MondayToFriday();

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, LocalIstanbul(Friday, 17), LocalIstanbul(NextMonday, 10));

        // Friday 17:00-18:00 (1h) + Monday 09:00-10:00 (1h); Saturday/Sunday contribute nothing.
        Assert.Equal(2 * 3600L, seconds);
    }

    [Fact]
    public void Skips_a_holiday_that_falls_on_a_working_day()
    {
        var rules = MondayToFriday(Monday);

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, LocalIstanbul(Monday, 8), LocalIstanbul(new DateOnly(2026, 1, 6), 10));

        // Monday is a holiday (0h); Tuesday 09:00-10:00 (1h).
        Assert.Equal(1 * 3600L, seconds);
    }

    [Fact]
    public void From_after_to_returns_zero()
    {
        var rules = MondayToFriday();

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, LocalIstanbul(Monday, 14), LocalIstanbul(Monday, 10));

        Assert.Equal(0L, seconds);
    }

    [Fact]
    public void Adding_seconds_that_exhaust_friday_afternoon_rolls_over_to_monday_morning()
    {
        var rules = MondayToFriday();

        var result = BusinessTimeCalculator.AddSeconds(rules, LocalIstanbul(Friday, 17, 30), 60 * 60);

        Assert.Equal(LocalIstanbul(NextMonday, 9, 30), result);
    }

    [Fact]
    public void Adding_zero_seconds_to_a_moment_outside_hours_snaps_to_the_next_opening()
    {
        var rules = MondayToFriday();

        var result = BusinessTimeCalculator.AddSeconds(rules, LocalIstanbul(Saturday, 12), 0);

        Assert.Equal(LocalIstanbul(NextMonday, 9, 0), result);
    }

    [Fact]
    public void Adding_zero_seconds_inside_working_hours_returns_the_same_instant()
    {
        var rules = MondayToFriday();
        var start = LocalIstanbul(Monday, 11);

        var result = BusinessTimeCalculator.AddSeconds(rules, start, 0);

        Assert.Equal(start, result);
    }

    [Fact]
    public void A_calendar_with_no_working_day_refuses_to_calculate()
    {
        var rules = new BusinessCalendarRules(Istanbul, new Dictionary<DayOfWeek, WorkingRange>(), new HashSet<DateOnly>());

        var betweenError = Assert.Throws<DomainException>(() => BusinessTimeCalculator.SecondsBetween(rules, LocalIstanbul(Monday, 9), LocalIstanbul(Monday, 10)));
        var addError = Assert.Throws<DomainException>(() => BusinessTimeCalculator.AddSeconds(rules, LocalIstanbul(Monday, 9), 60));

        Assert.Equal(ErrorCodes.SlaCalendarEmpty, betweenError.Code);
        Assert.Equal(ErrorCodes.SlaCalendarEmpty, addError.Code);
    }

    [Fact]
    public void Dst_spring_forward_gap_shortens_a_working_range_that_spans_it()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var rules = new BusinessCalendarRules(
            berlin,
            new Dictionary<DayOfWeek, WorkingRange> { [DayOfWeek.Sunday] = new(new TimeOnly(1, 0), new TimeOnly(4, 0)) },
            new HashSet<DateOnly>());

        // 2026-03-29 is the last Sunday of March 2026: EU clocks spring forward 02:00 CET -> 03:00 CEST.
        var fromUtc = new DateTimeOffset(2026, 3, 28, 23, 0, 0, TimeSpan.Zero); // local 2026-03-29 00:00 CET
        var toUtc = new DateTimeOffset(2026, 3, 29, 22, 0, 0, TimeSpan.Zero); // local 2026-03-30 00:00 CEST

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, fromUtc, toUtc);

        // Wall-clock window is 01:00-04:00 (nominally 3h), but 02:00-03:00 never occurred that day.
        Assert.Equal(2 * 3600L, seconds);
    }

    [Fact]
    public void AddSeconds_and_SecondsBetween_are_inverse_over_two_hundred_random_cases()
    {
        var rules = MondayToFriday();
        var random = new Random(20260927);
        var epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 200; i++)
        {
            var fromUtc = epoch.AddSeconds(random.Next(0, 60 * 60 * 24 * 60));
            var seconds = random.Next(0, 60 * 60 * 24 * 10);

            var toUtc = BusinessTimeCalculator.AddSeconds(rules, fromUtc, seconds);
            var measured = BusinessTimeCalculator.SecondsBetween(rules, fromUtc, toUtc);

            Assert.Equal(seconds, measured);
        }
    }

    // Ordinary Mon-Fri 09:00-18:00 Berlin calendar (unlike the Sunday-only gap calendar above): business hours
    // never overlap the 02:00-03:00 transition window itself, so these prove the brief's exact scenario — a
    // working range computed correctly on each side of a DST change keeps its wall-clock hours (spec §5.2).
    private static BusinessCalendarRules BerlinMondayToFriday() => new(
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"),
        new Dictionary<DayOfWeek, WorkingRange>
        {
            [DayOfWeek.Monday] = OfficeHours,
            [DayOfWeek.Tuesday] = OfficeHours,
            [DayOfWeek.Wednesday] = OfficeHours,
            [DayOfWeek.Thursday] = OfficeHours,
            [DayOfWeek.Friday] = OfficeHours,
        },
        new HashSet<DateOnly>());

    [Fact]
    public void Spring_forward_weekend_keeps_wall_clock_hours_across_cet_and_cest()
    {
        var rules = BerlinMondayToFriday();
        // 2026-03-29 is the last Sunday of March 2026 (CET -> CEST); Friday is still CET, the next Monday is CEST.
        var fromUtc = new DateTimeOffset(2026, 3, 27, 17, 0, 0, TimeSpan.FromHours(1));
        var toUtc = new DateTimeOffset(2026, 3, 30, 10, 0, 0, TimeSpan.FromHours(2));

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, fromUtc, toUtc);

        // Friday 17:00-18:00 CET (1h) + Monday 09:00-10:00 CEST (1h).
        Assert.Equal(2 * 3600L, seconds);
    }

    [Fact]
    public void AddSeconds_across_the_spring_forward_weekend_lands_on_monday_in_cest()
    {
        var rules = BerlinMondayToFriday();
        var fromUtc = new DateTimeOffset(2026, 3, 27, 17, 30, 0, TimeSpan.FromHours(1));

        var result = BusinessTimeCalculator.AddSeconds(rules, fromUtc, 60 * 60);

        Assert.Equal(new DateTimeOffset(2026, 3, 30, 9, 30, 0, TimeSpan.FromHours(2)), result);
    }

    [Fact]
    public void Fall_back_weekend_keeps_wall_clock_hours_across_cest_and_cet()
    {
        var rules = BerlinMondayToFriday();
        // 2026-10-25 is the last Sunday of October 2026 (CEST -> CET); Friday is still CEST, the next Monday is CET.
        var fromUtc = new DateTimeOffset(2026, 10, 23, 17, 0, 0, TimeSpan.FromHours(2));
        var toUtc = new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.FromHours(1));

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, fromUtc, toUtc);

        Assert.Equal(2 * 3600L, seconds);
    }

    [Fact]
    public void AddSeconds_across_the_fall_back_weekend_lands_on_monday_in_cet()
    {
        var rules = BerlinMondayToFriday();
        var fromUtc = new DateTimeOffset(2026, 10, 23, 17, 30, 0, TimeSpan.FromHours(2));

        var result = BusinessTimeCalculator.AddSeconds(rules, fromUtc, 60 * 60);

        Assert.Equal(new DateTimeOffset(2026, 10, 26, 9, 30, 0, TimeSpan.FromHours(1)), result);
    }

    [Fact]
    public void AddSeconds_and_SecondsBetween_are_inverse_over_two_hundred_random_cases_spanning_both_dst_transitions()
    {
        var rules = BerlinMondayToFriday();
        var random = new Random(20260928);
        var epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 200; i++)
        {
            // Up to ~300 days from 2026-01-01 covers both the March and the October transition.
            var fromUtc = epoch.AddSeconds(random.Next(0, 60 * 60 * 24 * 300));
            var seconds = random.Next(0, 60 * 60 * 24 * 10);

            var toUtc = BusinessTimeCalculator.AddSeconds(rules, fromUtc, seconds);
            var measured = BusinessTimeCalculator.SecondsBetween(rules, fromUtc, toUtc);

            Assert.Equal(seconds, measured);
        }
    }

    [Fact]
    public void AddSeconds_always_returns_a_utc_offset_regardless_of_the_input_offset()
    {
        var rules = MondayToFriday();
        var start = LocalIstanbul(Monday, 11); // constructed with a +03:00 offset

        var result = BusinessTimeCalculator.AddSeconds(rules, start, 0);

        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Fact]
    public void AddSeconds_returns_a_utc_offset_even_when_it_rolls_into_a_later_working_day()
    {
        var rules = MondayToFriday();

        var result = BusinessTimeCalculator.AddSeconds(rules, LocalIstanbul(Friday, 17, 30), 60 * 60);

        Assert.Equal(TimeSpan.Zero, result.Offset);
    }

    [Fact]
    public void SecondsBetween_does_not_refuse_a_range_longer_than_ten_years()
    {
        var rules = MondayToFriday();
        var from = LocalIstanbul(Monday, 10);
        var to = from.AddYears(11);

        var seconds = BusinessTimeCalculator.SecondsBetween(rules, from, to);

        Assert.True(seconds > 0);
    }
}
