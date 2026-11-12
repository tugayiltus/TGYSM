namespace Tgysm.Infrastructure.Persistence;

/// <summary>Last issued asset tag number per company calendar year (ADR-0029); written by <see cref="AssetTagGenerator"/> and the renumber step.</summary>
public sealed class AssetTagCounter
{
    public int Year { get; set; }
    public long LastValue { get; set; }
}
