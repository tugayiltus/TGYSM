using System.Net.Http.Json;
using System.Text.Json;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;

namespace Tgysm.Infrastructure.Identity;

/// <summary>Reads the tenant's OpenID metadata, then asks for an app token; Entra's error codes name the wrong field (ADR-0030).</summary>
public sealed class EntraConnectionTester(HttpClient http) : IEntraConnectionTester
{
    public const string Authority = "https://login.microsoftonline.com";

    /// <summary>The whole test gives up after this; a silent endpoint must not hold the admin page for minutes.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private const string MetadataPath = "/v2.0/.well-known/openid-configuration";
    private const string TokenEndpointField = "token_endpoint";
    private const string ErrorCodesField = "error_codes";
    private const string GraphDefaultScope = "https://graph.microsoft.com/.default";
    private const string ClientCredentials = "client_credentials";
    /// <summary>AADSTS7000215 / AADSTS7000222: the client secret is wrong or expired.</summary>
    private static readonly int[] SecretInvalid = [7000215, 7000222];

    public async Task<(bool Ok, string? ErrorCode)> TestAsync(string tenantId, string clientId, string clientSecret, CancellationToken callerToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        timeout.CancelAfter(RequestTimeout);
        var cancellationToken = timeout.Token;
        try
        {
            using var metadata = await http.GetAsync($"{Authority}/{Uri.EscapeDataString(tenantId)}{MetadataPath}", cancellationToken);
            if (!metadata.IsSuccessStatusCode)
            {
                return (false, ErrorCodes.AuthEntraTenantNotFound);
            }

            var document = await metadata.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var tokenEndpoint = document.GetProperty(TokenEndpointField).GetString();
            using var token = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["grant_type"] = ClientCredentials,
                ["scope"] = GraphDefaultScope,
            }), cancellationToken);
            if (token.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var error = await token.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var codes = error.TryGetProperty(ErrorCodesField, out var list) ? list.EnumerateArray().Select(c => c.GetInt32()).ToList() : [];
            // Any other refusal (AADSTS700016 and the rest) points at the application registration.
            return (false, codes.Any(SecretInvalid.Contains) ? ErrorCodes.AuthEntraSecretInvalid : ErrorCodes.AuthEntraClientInvalid);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || (e is OperationCanceledException && !callerToken.IsCancellationRequested))
        {
            return (false, ErrorCodes.AuthDirectoryUnavailable);
        }
    }
}
