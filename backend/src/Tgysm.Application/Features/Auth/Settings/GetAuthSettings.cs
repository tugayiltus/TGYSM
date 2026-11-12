using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;

namespace Tgysm.Application.Features.Auth.Settings;

public sealed class GetAuthSettingsHandler(IAppDbContext db, IOptionsMonitor<AppOptions> options)
{
    public async Task<AuthSettingsDto> HandleAsync(CancellationToken cancellationToken) =>
        AuthSettingsMapping.ToDto(await db.AuthSettings.AsNoTracking().SingleAsync(cancellationToken), options.CurrentValue.PublicBaseUrl);
}
