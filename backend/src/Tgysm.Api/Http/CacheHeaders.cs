namespace Tgysm.Api.Http;

internal static class CacheHeaders
{
    public const string NoStore = "no-store";

    /// <summary>For responses carrying one-time secrets (temporary passwords).</summary>
    public static void DisableCaching(HttpResponse response) => response.Headers.CacheControl = NoStore;
}
