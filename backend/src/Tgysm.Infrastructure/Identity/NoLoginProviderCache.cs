using Tgysm.Application.Abstractions;

namespace Tgysm.Infrastructure.Identity;

/// <summary>
/// Default for hosts without sign-in providers (the Worker): nothing is cached, so nothing is dropped. Tgysm.Api replaces it
/// with the cache of its OpenID Connect options (ADR-0030).
/// </summary>
internal sealed class NoLoginProviderCache : ILoginProviderCache
{
    public void Invalidate()
    {
    }
}
