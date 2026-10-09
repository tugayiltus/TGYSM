using Microsoft.AspNetCore.HttpOverrides;

namespace Tgysm.Api.Http;

public sealed class ProxyTrustOptions
{
    public const string SectionName = "Http";

    /// <summary>Networks (CIDR) of the reverse proxies whose forwarded headers are trusted outside Development (ADR-0024).</summary>
    public string[] KnownProxyNetworks { get; set; } = [];
}

/// <summary>Forwarded-header trust for hosting behind nginx: only the configured proxy networks may set the client address and scheme.</summary>
public static class ProxyTrust
{
    public static ForwardedHeadersOptions CreateForwardedHeadersOptions(ProxyTrustOptions trust)
    {
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
        // The defaults trust loopback only; behind a container proxy only the listed networks are trusted.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var network in trust.KnownProxyNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out var parsed))
            {
                throw new InvalidOperationException($"{ProxyTrustOptions.SectionName}:{nameof(ProxyTrustOptions.KnownProxyNetworks)} has an invalid network: '{network}'.");
            }

            options.KnownIPNetworks.Add(parsed);
        }

        return options;
    }
}
