namespace Tgysm.Application.Common;

/// <summary>Advisory lock keys for <see cref="Abstractions.IAppDbContext.RunSerializedAsync"/>; one key per cross-row rule.</summary>
public static class SerializationLocks
{
    /// <summary>Role changes and (de)activation of users: guards the "last active admin" rule.</summary>
    public const long AdminMembership = 7_001;

    /// <summary>Location moves: guards the "no cycles" rule.</summary>
    public const long LocationTree = 7_002;
}
