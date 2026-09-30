using Microsoft.AspNetCore.DataProtection;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Infrastructure.Security;

/// <summary><see cref="IAccountLinkProtector"/> over ASP.NET Core Data Protection (ADR-0015, ADR-0017), purpose-scoped to <see cref="OutboxEvent.ProtectedSecretPurpose"/>.</summary>
internal sealed class AccountLinkProtector : IAccountLinkProtector
{
    private readonly IDataProtector _protector;

    public AccountLinkProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(OutboxEvent.ProtectedSecretPurpose);

    public string Protect(string token) => _protector.Protect(token);

    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}
