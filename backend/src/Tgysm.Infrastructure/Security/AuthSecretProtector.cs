using Microsoft.AspNetCore.DataProtection;
using Tgysm.Application.Abstractions;

namespace Tgysm.Infrastructure.Security;

/// <summary><see cref="IAuthSecretProtector"/> over ASP.NET Core Data Protection with its own purpose (ADR-0030).</summary>
internal sealed class AuthSecretProtector(IDataProtectionProvider provider) : IAuthSecretProtector
{
    public const string Purpose = "Tgysm.AuthSettings";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string secret) => _protector.Protect(secret);

    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}
