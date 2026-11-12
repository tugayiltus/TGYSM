namespace Tgysm.Application.Abstractions;

/// <summary>
/// Proves Microsoft Entra settings before the method is switched on (ADR-0030): the tenant exists, and a client-credentials
/// token request with the client id and secret succeeds. <c>ErrorCode</c> is an <c>ErrorCodes</c> value on failure.
/// </summary>
public interface IEntraConnectionTester
{
    Task<(bool Ok, string? ErrorCode)> TestAsync(string tenantId, string clientId, string clientSecret, CancellationToken cancellationToken);
}
