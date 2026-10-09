namespace Tgysm.Application.Abstractions;

/// <summary>Next server-generated asset tag (<see cref="Domain.Assets.AssetTagFormat"/>); numbers are never reused.</summary>
public interface IAssetTagGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken);
}
