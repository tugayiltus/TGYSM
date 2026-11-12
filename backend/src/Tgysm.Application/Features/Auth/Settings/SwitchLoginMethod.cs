using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Auth;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth.Settings;

public sealed record SwitchLoginMethodCommand(LoginMethod Method, uint Version);

public sealed record LoginMethodSwitchPreview(int UsersWithoutPassword);

public sealed record LoginMethodDto(LoginMethod Method);

/// <summary>
/// Switches the active login method (ADR-0030). A directory method needs a passing test of its current settings. Back to
/// <see cref="LoginMethod.Local"/>, every active user without a local password gets a set-password link.
/// </summary>
public sealed class SwitchLoginMethodHandler(
    IAppDbContext db,
    PasswordTokenIssuer tokens,
    ILoginProviderCache providers,
    IAuditLog audit,
    IOptionsMonitor<AppOptions> options,
    TimeProvider clock)
{
    public async Task<AuthSettingsDto> HandleAsync(SwitchLoginMethodCommand command, CancellationToken cancellationToken)
    {
        var settings = await db.AuthSettings.SingleAsync(cancellationToken);
        db.SetExpectedVersion(settings, command.Version);
        var previous = settings.Method;
        settings.SwitchTo(command.Method, clock.GetUtcNow());
        var links = 0;
        if (command.Method == LoginMethod.Local && previous != LoginMethod.Local)
        {
            foreach (var userId in await WithoutPasswordQuery().Select(u => u.Id).ToListAsync(cancellationToken))
            {
                await tokens.IssueAsync(userId, PasswordTokenPurpose.Reset, cancellationToken);
                links++;
            }
        }

        audit.Record(AuditActions.LoginMethodChanged, AuditEntityTypes.AuthSettings, settings.Id,
            oldValues: new { Method = previous }, newValues: new { command.Method, SetPasswordLinks = links });
        await db.SaveChangesAsync(cancellationToken);
        providers.Invalidate();
        return AuthSettingsMapping.ToDto(await db.AuthSettings.AsNoTracking().SingleAsync(cancellationToken), options.CurrentValue.PublicBaseUrl);
    }

    public async Task<LoginMethodSwitchPreview> PreviewAsync(LoginMethod method, CancellationToken cancellationToken) =>
        new(method == LoginMethod.Local ? await WithoutPasswordQuery().CountAsync(cancellationToken) : 0);

    private IQueryable<User> WithoutPasswordQuery() => db.Users.Where(u => u.IsActive && u.PasswordHash == string.Empty);
}

/// <summary>The login page asks which form to show; anonymous, so it reveals only the method.</summary>
public sealed class GetLoginMethodHandler(IAppDbContext db)
{
    public async Task<LoginMethodDto> HandleAsync(CancellationToken cancellationToken) =>
        new(await db.AuthSettings.AsNoTracking().Select(s => s.Method).SingleAsync(cancellationToken));
}
