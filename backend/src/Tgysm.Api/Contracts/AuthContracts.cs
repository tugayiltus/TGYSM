namespace Tgysm.Api.Contracts;

public sealed record CsrfTokenResponse(string Token);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(bool MustChangePassword);
