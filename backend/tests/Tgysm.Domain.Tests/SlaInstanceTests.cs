using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests;

public sealed class SlaInstanceTests
{
    private const int FourHoursMinutes = 240;
    private const int OneHourMinutes = 60;
    private const int WarningPercent = 75;
    private const int SecondsPerHour = 3600;

    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly WorkingRange OfficeHours = new(new TimeOnly(9, 0), new TimeOnly(18, 0));
    private static readonly Guid TicketId = Guid.NewGuid();
    private static readonly Guid VersionId = Guid.NewGuid();

    // 2026-01-05 is a Monday; Istanbul is +03:00 all year.
    private static readonly DateOnly Monday = new(2026, 1, 5);

    private static readonly BusinessCalendarRules Rules = new(
        Istanbul,
        new Dictionary<DayOfWeek, WorkingRange>
        {
            [DayOfWeek.Monday] = OfficeHours,
            [DayOfWeek.Tuesday] = OfficeHours,
            [DayOfWeek.Wednesday] = OfficeHours,
            [DayOfWeek.Thursday] = OfficeHours,
            [DayOfWeek.Friday] = OfficeHours,
        },
        new HashSet<DateOnly>());

    private static DateTimeOffset At(DateOnly date, int hour, int minute = 0) =>
        new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(3)).ToUniversalTime();

    private static SlaTarget Target(int minutes = FourHoursMinutes) =>
        SlaTarget.Create(VersionId, TicketKind.Incident, Priority.P3, null, SlaMetric.Resolution, minutes, WarningPercent);

    private static SlaInstance Start(DateTimeOffset now, int minutes = FourHoursMinutes, bool paused = false, HoldReason? reason = null) =>
        SlaInstance.Start(TicketId, SlaMetric.Resolution, Target(minutes), VersionId, now, Rules, paused, reason);

    [Fact]
    public void Start_running_sets_due_and_warn_from_business_time()
    {
        var instance = Start(At(Monday, 10));

        Assert.Equal(At(Monday, 14), instance.DueAt);
        Assert.Equal(At(Monday, 13), instance.WarnAt);
        Assert.Equal(At(Monday, 10), instance.RunningSince);
        Assert.Equal(At(Monday, 10), instance.StartedAt);
        Assert.Equal(0, instance.ElapsedSeconds);
        Assert.Contains(instance.Events, e => e.Kind == SlaEventKind.Started);
    }

    [Fact]
    public void Start_paused_has_no_due_and_an_open_pause()
    {
        var instance = Start(At(Monday, 10), paused: true, reason: HoldReason.ManagerApproval);

        Assert.True(instance.IsPaused);
        Assert.Null(instance.DueAt);
        Assert.Null(instance.WarnAt);
        Assert.Null(instance.RunningSince);
        var pause = Assert.Single(instance.Pauses);
        Assert.Equal(HoldReason.ManagerApproval, pause.HoldReason);
        Assert.Null(pause.EndedAt);
    }

    [Fact]
    public void Pause_and_resume_accumulate_only_business_time()
    {
        var instance = Start(At(Monday, 16));

        // 2 business hours elapse Monday 16:00-18:00 and 30 minutes Tuesday 09:00-09:30; the night counts for nothing.
        instance.Pause(HoldReason.User, At(new DateOnly(2026, 1, 6), 9, 30), Rules);

        Assert.Equal((2 * SecondsPerHour) + (SecondsPerHour / 2), instance.ElapsedSeconds);
        Assert.Null(instance.DueAt);

        instance.Resume(At(new DateOnly(2026, 1, 6), 12), Rules);

        // 1.5 h remain, from Tuesday 12:00.
        Assert.Equal(At(new DateOnly(2026, 1, 6), 13, 30), instance.DueAt);
        Assert.Equal((2 * SecondsPerHour) + (SecondsPerHour / 2), instance.ElapsedSeconds);
        Assert.NotNull(Assert.Single(instance.Pauses).EndedAt);
        Assert.Contains(instance.Events, e => e.Kind == SlaEventKind.Paused);
        Assert.Contains(instance.Events, e => e.Kind == SlaEventKind.Resumed);
    }

    [Fact]
    public void Pause_when_already_paused_changes_nothing()
    {
        var instance = Start(At(Monday, 10));
        instance.Pause(HoldReason.User, At(Monday, 11), Rules);

        instance.Pause(HoldReason.Vendor, At(Monday, 12), Rules);

        Assert.Single(instance.Pauses);
        Assert.Equal(SecondsPerHour, instance.ElapsedSeconds);
    }

    [Fact]
    public void Change_target_while_running_keeps_elapsed_and_recomputes_due_from_the_start()
    {
        var instance = Start(At(Monday, 10));

        instance.ChangeTarget(OneHourMinutes * 8, WarningPercent, At(Monday, 12), Rules);

        // 8 h target, 2 h elapsed: 6 h remain from 12:00 -> Monday 18:00 holds 6 h.
        Assert.Equal(At(Monday, 18), instance.DueAt);
        Assert.Equal(2 * SecondsPerHour, instance.ElapsedSeconds);
        Assert.Equal(At(Monday, 10), instance.StartedAt);
        var changed = Assert.Single(instance.Events, e => e.Kind == SlaEventKind.TargetChanged);
        Assert.Contains("\"oldMinutes\":240", changed.DataJson);
        Assert.Contains("\"newMinutes\":480", changed.DataJson);
    }

    [Fact]
    public void Change_target_while_paused_keeps_elapsed_and_sets_due_on_resume()
    {
        var instance = Start(At(Monday, 10));
        instance.Pause(HoldReason.User, At(Monday, 11), Rules);

        instance.ChangeTarget(OneHourMinutes * 2, WarningPercent, At(Monday, 12), Rules);

        Assert.Null(instance.DueAt);
        Assert.Equal(SecondsPerHour, instance.ElapsedSeconds);

        instance.Resume(At(Monday, 13), Rules);

        // 2 h target, 1 h elapsed: due one hour after the resume.
        Assert.Equal(At(Monday, 14), instance.DueAt);
    }

    [Fact]
    public void Change_target_below_elapsed_makes_due_not_later_than_now()
    {
        var instance = Start(At(Monday, 10));

        instance.ChangeTarget(OneHourMinutes, WarningPercent, At(Monday, 13), Rules);

        Assert.True(instance.DueAt <= At(Monday, 13));
    }

    [Fact]
    public void Stop_within_the_target_is_met()
    {
        var instance = Start(At(Monday, 10));

        instance.Stop(null, At(Monday, 13), Rules);

        Assert.Equal(SlaResult.Met, instance.Result);
        Assert.Equal(At(Monday, 13), instance.StoppedAt);
        Assert.Null(instance.RunningSince);
        Assert.Equal(3 * SecondsPerHour, instance.ElapsedSeconds);
        Assert.Contains(instance.Events, e => e.Kind == SlaEventKind.Stopped);
    }

    [Fact]
    public void Stop_after_due_is_breached()
    {
        var instance = Start(At(Monday, 10));

        instance.Stop(null, At(Monday, 15), Rules);

        Assert.Equal(SlaResult.Breached, instance.Result);
    }

    [Fact]
    public void A_recorded_breach_stays_after_the_target_is_raised()
    {
        var instance = Start(At(Monday, 10));
        instance.RecordBreach(At(Monday, 14));

        instance.ChangeTarget(OneHourMinutes * 8, WarningPercent, At(Monday, 15), Rules);
        instance.Stop(null, At(Monday, 16), Rules);

        Assert.Equal(At(Monday, 14), instance.BreachedAt);
        Assert.Equal(SlaResult.Breached, instance.Result);
    }

    [Fact]
    public void Stop_with_an_explicit_result_keeps_it_and_closes_an_open_pause()
    {
        var instance = Start(At(Monday, 10));
        instance.Pause(HoldReason.User, At(Monday, 11), Rules);

        instance.Stop(SlaResult.Cancelled, At(Monday, 12), Rules);

        Assert.Equal(SlaResult.Cancelled, instance.Result);
        Assert.Equal(At(Monday, 12), Assert.Single(instance.Pauses).EndedAt);
        Assert.Equal(SecondsPerHour, instance.ElapsedSeconds);
    }

    [Fact]
    public void A_stopped_instance_rejects_further_changes()
    {
        var instance = Start(At(Monday, 10));
        instance.Stop(null, At(Monday, 11), Rules);

        Assert.Throws<InvalidOperationException>(() => instance.Pause(HoldReason.User, At(Monday, 12), Rules));
        Assert.Throws<InvalidOperationException>(() => instance.Stop(null, At(Monday, 12), Rules));
        Assert.Throws<InvalidOperationException>(() => instance.ChangeTarget(OneHourMinutes, WarningPercent, At(Monday, 12), Rules));
    }

    [Fact]
    public void Resume_when_running_is_rejected()
    {
        var instance = Start(At(Monday, 10));

        Assert.Throws<InvalidOperationException>(() => instance.Resume(At(Monday, 11), Rules));
    }

    [Fact]
    public void Record_warning_is_set_once()
    {
        var instance = Start(At(Monday, 10));

        instance.RecordWarning(At(Monday, 13));
        instance.RecordWarning(At(Monday, 13).AddMinutes(1));

        Assert.Equal(At(Monday, 13), instance.WarnedAt);
        Assert.Single(instance.Events, e => e.Kind == SlaEventKind.Warning);
    }

    [Fact]
    public void Resume_on_a_closed_weekend_day_pushes_the_remaining_time_into_the_next_week()
    {
        var friday = Monday.AddDays(4);
        var instance = Start(At(friday, 10), minutes: OneHourMinutes * 8);
        instance.Pause(HoldReason.User, At(friday, 12), Rules);

        instance.Resume(At(friday.AddDays(1), 10), Rules);

        // 2 h used on Friday, 6 h left: the closed Saturday and Sunday count nothing, so they run Monday 09:00-15:00.
        Assert.Equal(2 * SecondsPerHour, instance.ElapsedSeconds);
        Assert.Equal(At(friday.AddDays(3), 15), instance.DueAt);
    }

    [Fact]
    public void Running_across_a_weekend_counts_only_business_time()
    {
        var friday = Monday.AddDays(4);
        var instance = Start(At(friday, 16), minutes: OneHourMinutes * 8);

        instance.Pause(HoldReason.User, At(friday.AddDays(3), 10), Rules);

        // Friday 16:00-18:00 (2 h) plus Monday 09:00-10:00 (1 h).
        Assert.Equal(3 * SecondsPerHour, instance.ElapsedSeconds);
    }

    [Fact]
    public void Resume_before_a_holiday_pushes_the_remaining_time_past_it()
    {
        var holiday = Monday.AddDays(1);
        var rules = Rules with { Holidays = new HashSet<DateOnly> { holiday } };
        var instance = SlaInstance.Start(TicketId, SlaMetric.Resolution, Target(OneHourMinutes * 8), VersionId, At(Monday, 10), rules, paused: false, pauseReason: null);
        instance.Pause(HoldReason.User, At(Monday, 16), rules);

        instance.Resume(At(Monday, 17), rules);

        // 6 h used, 2 h left: Monday 17:00-18:00 (1 h), the holiday Tuesday counts nothing, Wednesday 09:00-10:00 (1 h).
        Assert.Equal(6 * SecondsPerHour, instance.ElapsedSeconds);
        Assert.Equal(At(Monday.AddDays(2), 10), instance.DueAt);
    }

    [Fact]
    public void Running_across_a_holiday_counts_only_business_time()
    {
        var holiday = Monday.AddDays(1);
        var rules = Rules with { Holidays = new HashSet<DateOnly> { holiday } };
        var instance = SlaInstance.Start(TicketId, SlaMetric.Resolution, Target(OneHourMinutes * 8), VersionId, At(Monday, 17), rules, paused: false, pauseReason: null);

        instance.Pause(HoldReason.User, At(Monday.AddDays(2), 10), rules);

        // Monday 17:00-18:00 (1 h) plus Wednesday 09:00-10:00 (1 h).
        Assert.Equal(2 * SecondsPerHour, instance.ElapsedSeconds);
    }

    [Fact]
    public void Change_target_to_a_later_warning_resets_the_warned_mark()
    {
        var instance = Start(At(Monday, 10));
        instance.RecordWarning(At(Monday, 13));

        instance.ChangeTarget(OneHourMinutes * 8, WarningPercent, At(Monday, 13, 30), Rules);

        Assert.Null(instance.WarnedAt);
        Assert.True(instance.WarnAt > At(Monday, 13, 30));
    }

    [Fact]
    public void Change_target_whose_warning_is_already_passed_keeps_the_warned_mark()
    {
        var instance = Start(At(Monday, 10));
        instance.RecordWarning(At(Monday, 13));

        instance.ChangeTarget(OneHourMinutes, WarningPercent, At(Monday, 13, 30), Rules);

        Assert.NotNull(instance.WarnedAt);
    }

    [Fact]
    public void Raising_the_target_while_paused_lets_the_instance_warn_again_after_resume()
    {
        var instance = Start(At(Monday, 10));
        instance.RecordWarning(At(Monday, 13));
        instance.Pause(HoldReason.User, At(Monday, 13, 30), Rules);

        instance.ChangeTarget(OneHourMinutes * 8, WarningPercent, At(Monday, 14), Rules);
        Assert.Null(instance.WarnedAt);

        instance.Resume(At(Monday, 15), Rules);

        Assert.Null(instance.WarnedAt);
        Assert.True(instance.WarnAt > At(Monday, 15));
    }
}
