namespace Tgysm.Application.Abstractions;

/// <summary>Next server-generated asset tag of a company calendar year (<see cref="Domain.Assets.AssetTagFormat"/>); numbers are never reused.</summary>
public interface IAssetTagGenerator
{
    Task<string> NextAsync(int year, CancellationToken cancellationToken);
}
