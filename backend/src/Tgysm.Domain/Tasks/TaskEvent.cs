using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tasks;

/// <summary>Append-only task history row. <see cref="DataJson"/> holds kind-specific details (no secrets).</summary>
public sealed class TaskEvent
{
    private TaskEvent()
    {
    }

    // Sequential: the history orders by time, then id, and several events share one instant.
    public Guid Id { get; private init; } = SequentialId.Next();
    public Guid TaskId { get; private init; }
    public TaskEventKind Kind { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string DataJson { get; private init; } = "{}";
    public DateTimeOffset OccurredAt { get; private init; }

    public static TaskEvent Record(Guid taskId, TaskEventKind kind, Guid? actorUserId, string dataJson, DateTimeOffset now) =>
        new() { TaskId = taskId, Kind = kind, ActorUserId = actorUserId, DataJson = dataJson, OccurredAt = now };
}
