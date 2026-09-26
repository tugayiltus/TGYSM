namespace Tgysm.Domain.Assets;

public enum AssetMovementKind
{
    Created,
    Updated,
    Assigned,
    Returned,
    Transferred,
    MaintenanceStarted,
    MaintenanceEnded,
    Lost,
    Found,
    Retired,
}
