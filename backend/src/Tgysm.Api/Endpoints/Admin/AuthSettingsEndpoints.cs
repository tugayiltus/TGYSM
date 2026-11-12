using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Features.Auth.Settings;
using Tgysm.Domain.Auth;

namespace Tgysm.Api.Endpoints.Admin;

public sealed record UpdateEntraSettingsRequest(string TenantId, string ClientId, string? ClientSecret, IReadOnlyList<string> AllowedDomains, uint Version);

public sealed record UpdateLdapSettingsRequest(string Host, int Port, LdapSecurity Security, string BaseDn, string BindDn, string? BindPassword, string? UserFilter, uint Version);

/// <summary>Login method settings (ADR-0030); admin only through the admin group.</summary>
internal static class AuthSettingsEndpoints
{
    private const string Tag = "Admin: Login method";

    public static RouteGroupBuilder MapAuthSettingsEndpoints(this RouteGroupBuilder admin)
    {
        var settings = admin.MapGroup(ApiRoutes.AuthSettings).WithTags(Tag);
        settings.MapGet("", async (GetAuthSettingsHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)));
        settings.MapPut("/entra", UpdateEntraAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        settings.MapPut("/ldap", UpdateLdapAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        return admin;
    }

    private static async Task<Ok<AuthSettingsDto>> UpdateEntraAsync(UpdateEntraSettingsRequest request, UpdateAuthSettingsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(new UpdateEntraSettingsCommand(request.TenantId, request.ClientId, request.ClientSecret, request.AllowedDomains, request.Version), ct));

    private static async Task<Ok<AuthSettingsDto>> UpdateLdapAsync(UpdateLdapSettingsRequest request, UpdateAuthSettingsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(new UpdateLdapSettingsCommand(request.Host, request.Port, request.Security, request.BaseDn, request.BindDn, request.BindPassword, request.UserFilter, request.Version), ct));
}
