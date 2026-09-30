namespace Tgysm.Application.Common;

/// <summary>
/// A password-set link token is unknown, already used, superseded, expired, or belongs to an inactive account.
/// Always surfaces as 400 <c>auth.token_invalid</c>: one code for every case so the error is not an oracle (ADR-0017).
/// </summary>
public sealed class PasswordTokenInvalidException() : Exception("Password token is invalid.");
