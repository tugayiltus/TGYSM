namespace Tgysm.Domain.Sla;

/// <summary>One weekday's working window in local wall-clock time (ADR-0018).</summary>
public sealed record WorkingRange(TimeOnly Start, TimeOnly End)
{
    public TimeOnly Start { get; } = Start < End
        ? Start
        : throw new ArgumentException("Working range start must be before its end.", nameof(Start));

    public TimeOnly End { get; } = End;
}
