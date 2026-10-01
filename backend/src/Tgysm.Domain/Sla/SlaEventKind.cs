namespace Tgysm.Domain.Sla;

/// <summary>Kinds of the append-only <see cref="SlaEvent"/> log (spec §5.1).</summary>
public enum SlaEventKind
{
    Started,
    Paused,
    Resumed,
    TargetChanged,
    Warning,
    Breached,
    Stopped,
    CalendarChanged,
}
