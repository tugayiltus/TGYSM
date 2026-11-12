namespace Tgysm.Application.Abstractions;

/// <summary>Drops cached sign-in provider options after the login settings change, so no restart is needed (ADR-0030).</summary>
public interface ILoginProviderCache
{
    void Invalidate();
}
