namespace Tgysm.Domain.Assets;

/// <summary>What a lifecycle operation produced; the handler persists both.</summary>
public sealed record AssetChange(AssetMovement Movement, AssetAssignment? OpenedAssignment = null);
