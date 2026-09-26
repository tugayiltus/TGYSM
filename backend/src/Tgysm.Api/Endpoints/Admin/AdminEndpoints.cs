using Tgysm.Api.Http;
using Tgysm.Api.Security;

namespace Tgysm.Api.Endpoints.Admin;

internal static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup(ApiRoutes.AdminGroup).RequireAuthorization(Policies.Admin);
        admin.MapUserEndpoints();
        admin.MapDepartmentEndpoints();
        admin.MapLocationEndpoints();
        admin.MapTeamEndpoints();
        admin.MapAuditEndpoints();
        return admin;
    }
}
