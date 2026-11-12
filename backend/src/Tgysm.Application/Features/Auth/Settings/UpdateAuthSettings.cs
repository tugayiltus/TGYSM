using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Auth;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Auth.Settings;

/// <summary>A null secret keeps the stored one: the page never shows secrets, so an empty field means "unchanged".</summary>
public sealed record UpdateEntraSettingsCommand(string TenantId, string ClientId, string? ClientSecret, IReadOnlyList<string> AllowedDomains, uint Version);

public sealed record UpdateLdapSettingsCommand(string Host, int Port, LdapSecurity Security, string BaseDn, string BindDn, string? BindPassword, string? UserFilter, uint Version);

internal static partial class AuthSettingsRules
{
    public const int MinPort = 1;
    public const int MaxPort = 65535;

    public static bool IsDomain(string value) => DomainPattern().IsMatch(value.Trim().TrimStart('@').ToLowerInvariant());

    [GeneratedRegex(@"^[a-z0-9-]+(\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();
}

internal sealed class UpdateEntraSettingsValidator : AbstractValidator<UpdateEntraSettingsCommand>
{
    public UpdateEntraSettingsValidator()
    {
        RuleFor(c => c.TenantId).Must(t => Guid.TryParse(t, out _));
        RuleFor(c => c.ClientId).NotEmpty().MaximumLength(AuthSettings.TextMaxLength);
        RuleFor(c => c.ClientSecret).MaximumLength(AuthSettings.TextMaxLength);
        // The allowed domains are what keep invited guests out (ADR-0030), so at least one is required.
        RuleFor(c => c.AllowedDomains).NotEmpty();
        RuleForEach(c => c.AllowedDomains).Must(AuthSettingsRules.IsDomain);
    }
}

internal sealed class UpdateLdapSettingsValidator : AbstractValidator<UpdateLdapSettingsCommand>
{
    public UpdateLdapSettingsValidator()
    {
        RuleFor(c => c.Host).NotEmpty().MaximumLength(AuthSettings.HostMaxLength);
        RuleFor(c => c.Port).InclusiveBetween(AuthSettingsRules.MinPort, AuthSettingsRules.MaxPort);
        RuleFor(c => c.Security).IsInEnum();
        RuleFor(c => c.BaseDn).NotEmpty().MaximumLength(AuthSettings.TextMaxLength);
        RuleFor(c => c.BindDn).NotEmpty().MaximumLength(AuthSettings.TextMaxLength);
        RuleFor(c => c.BindPassword).MaximumLength(AuthSettings.TextMaxLength);
        RuleFor(c => c.UserFilter).MaximumLength(AuthSettings.TextMaxLength);
    }
}

public sealed class UpdateAuthSettingsHandler(
    IAppDbContext db,
    IAuthSecretProtector secrets,
    IAuditLog audit,
    IOptionsMonitor<AppOptions> options,
    ILoginProviderCache providers,
    LoginProviderTester tester,
    IValidator<UpdateEntraSettingsCommand> entraValidator,
    IValidator<UpdateLdapSettingsCommand> ldapValidator,
    TimeProvider clock)
{
    public async Task<AuthSettingsDto> HandleAsync(UpdateEntraSettingsCommand command, CancellationToken cancellationToken)
    {
        await entraValidator.ValidateAndThrowAsync(command, cancellationToken);
        var settings = await LoadAsync(command.Version, cancellationToken);
        settings.UpdateEntra(command.TenantId, command.ClientId, Protect(command.ClientSecret), command.AllowedDomains, clock.GetUtcNow());
        // Secrets never go into audit: only which fields were sent.
        audit.Record(AuditActions.AuthSettingsUpdated, AuditEntityTypes.AuthSettings, settings.Id,
            newValues: new { Provider = LoginMethod.Entra, settings.EntraTenantId, settings.EntraClientId, settings.EntraAllowedDomains, ClientSecretChanged = command.ClientSecret is not null });
        return await SaveAsync(settings, LoginMethod.Entra, cancellationToken);
    }

    public async Task<AuthSettingsDto> HandleAsync(UpdateLdapSettingsCommand command, CancellationToken cancellationToken)
    {
        await ldapValidator.ValidateAndThrowAsync(command, cancellationToken);
        var settings = await LoadAsync(command.Version, cancellationToken);
        settings.UpdateLdap(command.Host, command.Port, command.Security, command.BaseDn, command.BindDn, Protect(command.BindPassword), command.UserFilter, clock.GetUtcNow());
        audit.Record(AuditActions.AuthSettingsUpdated, AuditEntityTypes.AuthSettings, settings.Id,
            newValues: new { Provider = LoginMethod.Ldap, settings.LdapHost, settings.LdapPort, settings.LdapSecurity, settings.LdapBaseDn, settings.LdapBindDn, settings.LdapUserFilter, BindPasswordChanged = command.BindPassword is not null });
        return await SaveAsync(settings, LoginMethod.Ldap, cancellationToken);
    }

    private string? Protect(string? secret) => string.IsNullOrEmpty(secret) ? null : secrets.Protect(secret);

    private async Task<AuthSettings> LoadAsync(uint version, CancellationToken cancellationToken)
    {
        var settings = await db.AuthSettings.SingleAsync(cancellationToken);
        db.SetExpectedVersion(settings, version);
        return settings;
    }

    /// <summary>
    /// Settings of the active method go live on save, so they must pass a connection test first; a failing test refuses the
    /// save and the last working settings stay in use. Settings of an inactive method are saved untested (ADR-0030).
    /// </summary>
    private async Task<AuthSettingsDto> SaveAsync(AuthSettings settings, LoginMethod provider, CancellationToken cancellationToken)
    {
        if (settings.Method == provider)
        {
            var test = await tester.TestAsync(settings, provider, cancellationToken);
            if (!test.Ok)
            {
                throw new DomainException(test.ErrorCode ?? ErrorCodes.AuthDirectoryUnavailable, "The new settings of the active login method failed the connection test.");
            }

            settings.MarkTested(provider);
        }

        await db.SaveChangesAsync(cancellationToken);
        providers.Invalidate();
        var saved = await db.AuthSettings.AsNoTracking().SingleAsync(cancellationToken);
        return AuthSettingsMapping.ToDto(saved, options.CurrentValue.PublicBaseUrl);
    }
}
