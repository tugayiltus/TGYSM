using Tgysm.Domain.Common;

namespace Tgysm.Domain.Changes;

/// <summary>Append-only change history row. <see cref="DataJson"/> holds kind-specific details (no secrets).</summary>
public sealed class ChangeEvent
{
    private ChangeEvent()
    {
    }

    // Sequential: the history orders by time, then id, and several events share one instant.
    public Guid Id { get; private init; } = SequentialId.Next();
    public Guid ChangeId { get; private init; }
    public ChangeEventKind Kind { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string DataJson { get; private init; } = "{}";
    public DateTimeOffset OccurredAt { get; private init; }

    public static ChangeEvent Record(Guid changeId, ChangeEventKind kind, Guid? actorUserId, string dataJson, DateTimeOffset now) =>
        new() { ChangeId = changeId, Kind = kind, ActorUserId = actorUserId, DataJson = dataJson, OccurredAt = now };
}
