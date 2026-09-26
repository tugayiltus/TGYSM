namespace Tgysm.Api.Http;

internal static class ApiRoutes
{
    public const string ApiPrefix = "/api";
    public const string V1 = ApiPrefix + "/v1";
    public const string Auth = "/auth";
    public const string AdminGroup = "/admin";
    public const string Admin = V1 + AdminGroup;
    public const string Users = "/users";
    public const string Departments = "/departments";
    public const string Locations = "/locations";
    public const string Teams = "/teams";
    public const string AuditEvents = "/audit-events";
}
