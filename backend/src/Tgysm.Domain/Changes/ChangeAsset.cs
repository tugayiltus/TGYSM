namespace Tgysm.Domain.Changes;

/// <summary>Change ↔ affected asset link.</summary>
public sealed class ChangeAsset
{
    private ChangeAsset()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ChangeId { get; private init; }
    public Guid AssetId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static ChangeAsset Create(Guid changeId, Guid assetId, DateTimeOffset now) =>
        new() { ChangeId = changeId, AssetId = assetId, CreatedAt = now };
}
