namespace Tgysm.Domain.Assets;

/// <summary>Lifecycle states (ADR-0010). Only lifecycle operations change the status.</summary>
public enum AssetStatus
{
    InStock,
    Assigned,
    Maintenance,
    Lost,
    Retired,
}
