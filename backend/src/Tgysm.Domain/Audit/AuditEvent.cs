namespace Tgysm.Domain.Audit;

/// <summary>Append-only audit record (database trigger rejects UPDATE/DELETE).</summary>
public sealed class AuditEvent
{
    public const int ActionMaxLength = 100;
    public const int EntityTypeMaxLength = 50;
    public const int ReasonMaxLength = 500;
    public const int CorrelationIdMaxLength = 100;

    private AuditEvent()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string Action { get; private init; } = string.Empty;
    public string EntityType { get; private init; } = string.Empty;
    public Guid? EntityId { get; private init; }
    public string? Reason { get; private init; }

    /// <summary>JSON snapshot before the change (jsonb).</summary>
    public string? OldValues { get; private init; }

    /// <summary>JSON snapshot after the change (jsonb).</summary>
    public string? NewValues { get; private init; }

    public string CorrelationId { get; private init; } = string.Empty;

    public static AuditEvent Create(
        DateTimeOffset occurredAt,
        Guid? actorUserId,
        string action,
        string entityType,
        Guid? entityId,
        string? reason,
        string? oldValues,
        string? newValues,
        string correlationId) => new()
        {
            OccurredAt = occurredAt,
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Reason = reason,
            OldValues = oldValues,
            NewValues = newValues,
            CorrelationId = correlationId,
        };
}
