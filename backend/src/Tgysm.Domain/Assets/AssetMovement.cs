namespace Tgysm.Domain.Assets;

/// <summary>Append-only history row of an asset (ADR-0010).</summary>
public sealed class AssetMovement
{
    public const int ReasonMaxLength = 500;

    private AssetMovement()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid AssetId { get; private init; }
    public AssetMovementKind Kind { get; private init; }
    public AssetStatus? FromStatus { get; private init; }
    public AssetStatus ToStatus { get; private init; }

    /// <summary>The user the movement concerns (receiver, returner), if any.</summary>
    public Guid? UserId { get; private init; }

    public Guid ActorUserId { get; private init; }
    public string? Reason { get; private init; }
    public bool? UserResponsibilityRetained { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }

    internal static AssetMovement Record(
        Guid assetId,
        AssetMovementKind kind,
        AssetStatus? fromStatus,
        AssetStatus toStatus,
        Guid? userId,
        Guid actorUserId,
        string? reason,
        bool? userResponsibilityRetained,
        DateTimeOffset occurredAt) => new()
        {
            AssetId = assetId,
            Kind = kind,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            UserId = userId,
            ActorUserId = actorUserId,
            Reason = reason,
            UserResponsibilityRetained = userResponsibilityRetained,
            OccurredAt = occurredAt,
        };
}
