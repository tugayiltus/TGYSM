namespace Tgysm.Domain.Audit;

/// <summary>Entity type names used in audit rows and NotFound errors. Turkish labels live under "entityTypes" in admin.json.</summary>
public static class AuditEntityTypes
{
    public const string User = "user";
    public const string Department = "department";
    public const string Location = "location";
    public const string Team = "team";
    public const string Asset = "asset";
    public const string AssetImport = "asset_import";
}
