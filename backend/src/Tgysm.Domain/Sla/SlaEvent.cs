namespace Tgysm.Domain.Sla;

/// <summary>Append-only log row of one SLA instance. <see cref="DataJson"/> holds kind-specific details (old/new target, hold reason).</summary>
public sealed class SlaEvent
{
    private SlaEvent()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid SlaInstanceId { get; private init; }

    public SlaEventKind Kind { get; private init; }

    public DateTimeOffset OccurredAt { get; private init; }

    public string DataJson { get; private init; } = "{}";

    public static SlaEvent Record(Guid slaInstanceId, SlaEventKind kind, DateTimeOffset now, string dataJson = "{}") =>
        new() { SlaInstanceId = slaInstanceId, Kind = kind, OccurredAt = now, DataJson = dataJson };
}
