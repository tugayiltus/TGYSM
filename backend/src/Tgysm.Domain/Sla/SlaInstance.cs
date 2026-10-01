using System.Text.Json;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Sla;

/// <summary>
/// One running timer of a ticket (ADR-0018, spec §5): first response or resolution. Business time accumulates in
/// <see cref="ElapsedSeconds"/> on pause, target change and stop; while running, <see cref="RunningSince"/> marks the
/// start of the stretch not yet added. <see cref="DueAt"/> and <see cref="WarnAt"/> are recomputed whenever the timer
/// (re)starts running or the target changes, and are null while paused.
/// </summary>
public sealed class SlaInstance : Entity
{
    private const int SecondsPerMinute = 60;
    private const int PercentBase = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<SlaPause> _pauses = [];
    private readonly List<SlaEvent> _events = [];

    private SlaInstance()
    {
    }

    public Guid TicketId { get; private init; }

    public SlaMetric Metric { get; private init; }

    /// <summary>The policy version this instance started on; its targets and pause rules apply for the instance's whole life.</summary>
    public Guid PolicyVersionId { get; private init; }

    public int TargetMinutes { get; private set; }

    public int WarningPercent { get; private set; }

    public DateTimeOffset StartedAt { get; private init; }

    public long ElapsedSeconds { get; private set; }

    public DateTimeOffset? RunningSince { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    public DateTimeOffset? WarnAt { get; private set; }

    public DateTimeOffset? WarnedAt { get; private set; }

    public DateTimeOffset? BreachedAt { get; private set; }

    public DateTimeOffset? StoppedAt { get; private set; }

    public SlaResult? Result { get; private set; }

    public IReadOnlyList<SlaPause> Pauses => _pauses;

    public IReadOnlyList<SlaEvent> Events => _events;

    public bool IsStopped => StoppedAt is not null;

    public bool IsPaused => !IsStopped && RunningSince is null;

    public static SlaInstance Start(
        Guid ticketId, SlaMetric metric, SlaTarget target, Guid policyVersionId, DateTimeOffset now, BusinessCalendarRules rules, bool paused, HoldReason? pauseReason)
    {
        var instance = new SlaInstance
        {
            TicketId = ticketId,
            Metric = metric,
            PolicyVersionId = policyVersionId,
            TargetMinutes = target.TargetMinutes,
            WarningPercent = target.WarningPercent,
            StartedAt = now,
        };
        instance.AddEvent(SlaEventKind.Started, now, new { target.TargetMinutes, paused });
        if (paused)
        {
            instance._pauses.Add(SlaPause.Begin(instance.Id, pauseReason ?? throw new ArgumentException("A paused start needs a hold reason.", nameof(pauseReason)), now));
            instance.AddEvent(SlaEventKind.Paused, now, new { holdReason = pauseReason });
        }
        else
        {
            instance.RunFrom(now, rules);
        }

        return instance;
    }

    /// <summary>Stops the clock for a hold; a no-op when the timer is already paused.</summary>
    public void Pause(HoldReason reason, DateTimeOffset now, BusinessCalendarRules rules)
    {
        EnsureNotStopped();
        if (RunningSince is not { } since)
        {
            return;
        }

        ElapsedSeconds += BusinessTimeCalculator.SecondsBetween(rules, since, now);
        RunningSince = null;
        DueAt = null;
        WarnAt = null;
        _pauses.Add(SlaPause.Begin(Id, reason, now));
        AddEvent(SlaEventKind.Paused, now, new { holdReason = reason });
    }

    public void Resume(DateTimeOffset now, BusinessCalendarRules rules)
    {
        EnsureNotStopped();
        if (RunningSince is not null)
        {
            throw new InvalidOperationException("The SLA instance is not paused.");
        }

        _pauses.FirstOrDefault(p => p.EndedAt is null)?.End(now);
        RunFrom(now, rules);
        AddEvent(SlaEventKind.Resumed, now);
    }

    /// <summary>Applies a new target (priority change): start and elapsed time are kept, an earlier breach stays recorded, and the warning mark resets while the new warning threshold is not yet used up.</summary>
    public void ChangeTarget(int targetMinutes, int warningPercent, DateTimeOffset now, BusinessCalendarRules rules)
    {
        EnsureNotStopped();
        var old = TargetMinutes;
        TargetMinutes = targetMinutes;
        WarningPercent = warningPercent;
        if (RunningSince is { } since)
        {
            ElapsedSeconds += BusinessTimeCalculator.SecondsBetween(rules, since, now);
            RunFrom(now, rules);
        }

        if (ElapsedSeconds < (long)TargetMinutes * SecondsPerMinute * WarningPercent / PercentBase)
        {
            // The new warning threshold is not yet used up (running or paused): a raised target can warn again.
            // An earlier breach stays recorded.
            WarnedAt = null;
        }

        AddEvent(SlaEventKind.TargetChanged, now, new { oldMinutes = old, newMinutes = targetMinutes });
    }

    /// <summary>Ends the timer; a null <paramref name="result"/> becomes met or breached from the elapsed time.</summary>
    public void Stop(SlaResult? result, DateTimeOffset now, BusinessCalendarRules rules)
    {
        EnsureNotStopped();
        if (RunningSince is { } since)
        {
            ElapsedSeconds += BusinessTimeCalculator.SecondsBetween(rules, since, now);
        }

        _pauses.FirstOrDefault(p => p.EndedAt is null)?.End(now);
        RunningSince = null;
        StoppedAt = now;
        Result = result ?? (BreachedAt is not null || ElapsedSeconds > (long)TargetMinutes * SecondsPerMinute ? SlaResult.Breached : SlaResult.Met);
        AddEvent(SlaEventKind.Stopped, now, new { result = Result });
    }

    /// <summary>Records the warning threshold being reached; set once.</summary>
    public void RecordWarning(DateTimeOffset now)
    {
        if (WarnedAt is not null)
        {
            return;
        }

        WarnedAt = now;
        AddEvent(SlaEventKind.Warning, now);
    }

    /// <summary>Records the due time being passed; set once, and kept when the target is later raised.</summary>
    public void RecordBreach(DateTimeOffset now)
    {
        if (BreachedAt is not null)
        {
            return;
        }

        BreachedAt = now;
        AddEvent(SlaEventKind.Breached, now);
    }

    private void RunFrom(DateTimeOffset now, BusinessCalendarRules rules)
    {
        RunningSince = now;
        DueAt = Moment(now, (long)TargetMinutes * SecondsPerMinute, rules);
        WarnAt = Moment(now, (long)TargetMinutes * SecondsPerMinute * WarningPercent / PercentBase, rules);
    }

    // The moment the given share of the target is used up, counted from the running stretch; not before it when already used up.
    private DateTimeOffset Moment(DateTimeOffset since, long shareSeconds, BusinessCalendarRules rules)
    {
        var remaining = shareSeconds - ElapsedSeconds;
        return remaining <= 0 ? since : BusinessTimeCalculator.AddSeconds(rules, since, remaining);
    }

    private void EnsureNotStopped()
    {
        if (IsStopped)
        {
            throw new InvalidOperationException("The SLA instance is already stopped.");
        }
    }

    private void AddEvent(SlaEventKind kind, DateTimeOffset now, object? data = null) =>
        _events.Add(SlaEvent.Record(Id, kind, now, data is null ? "{}" : JsonSerializer.Serialize(data, Json)));
}
