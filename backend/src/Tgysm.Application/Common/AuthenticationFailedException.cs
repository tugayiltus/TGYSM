namespace Tgysm.Application.Common;

/// <summary>Login or session failure. Always surfaces as 401 auth.invalid_credentials (no account enumeration).</summary>
public sealed class AuthenticationFailedException() : Exception("Authentication failed.");
