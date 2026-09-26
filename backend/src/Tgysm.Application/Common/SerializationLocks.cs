namespace Tgysm.Application.Common;

/// <summary>Advisory lock keys for <see cref="Abstractions.IAppDbContext.RunSerializedAsync"/>; one key per cross-row rule.</summary>
public static class SerializationLocks
{
    /// <summary>Role changes and (de)activation of users: guards the "last active admin" rule.</summary>
    public const long AdminMembership = 7_001;

    /// <summary>Location moves: guards the "no cycles" rule.</summary>
    public const long LocationTree = 7_002;

    /// <summary>Asset lifecycle operations: gives each handover its transaction (close then open) and serializes them.</summary>
    public const long AssetLifecycle = 7_003;

    /// <summary>Commits of CSV import jobs, so one job cannot insert its rows twice.</summary>
    public const long AssetImport = 7_004;

    /// <summary>Moving the default intake flag between teams.</summary>
    public const long DefaultIntake = 7_005;
}
