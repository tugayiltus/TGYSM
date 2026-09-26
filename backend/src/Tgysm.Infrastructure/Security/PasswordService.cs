using Microsoft.AspNetCore.Identity;
using Tgysm.Application.Abstractions;

namespace Tgysm.Infrastructure.Security;

/// <summary>PBKDF2 hashing via ASP.NET Core Identity's PasswordHasher (ADR-0004).</summary>
public sealed class PasswordService : IPasswordService
{
    // PasswordHasher<T> ignores the user argument in its default implementation.
    private static readonly object HashSubject = new();

    private readonly PasswordHasher<object> _hasher = new();
    private readonly Lazy<string> _dummyHash;

    public PasswordService() =>
        _dummyHash = new Lazy<string>(() => _hasher.HashPassword(HashSubject, Guid.NewGuid().ToString()));

    public string DummyHash => _dummyHash.Value;

    public string Hash(string password) => _hasher.HashPassword(HashSubject, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(HashSubject, passwordHash, password) != PasswordVerificationResult.Failed;
}
