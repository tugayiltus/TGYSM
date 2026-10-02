using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class ReportEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string IncidentsPath = "/api/v1/reports/incidents";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Staff_gets_the_incident_report_with_all_figures()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "staff@example.com");

        using var response = await client.GetAsync($"{IncidentsPath}?from=2026-03-01&to=2026-03-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await client.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("incidents", body.GetProperty("report").GetString());
        Assert.Equal("teams", body.GetProperty("scopeLabel").GetString());
        var keys = body.GetProperty("figures").EnumerateArray().Select(f => f.GetProperty("metric").GetString()).ToArray();
        Assert.Equal(["volume", "first_response_time", "resolution_time", "mttr", "open_backlog", "sla_compliance", "excluded_outcomes"], keys);
        var compliance = body.GetProperty("figures").EnumerateArray().Single(f => f.GetProperty("metric").GetString() == "sla_compliance");
        Assert.Equal(JsonValueKind.Null, compliance.GetProperty("value").ValueKind);
        Assert.Equal(0, compliance.GetProperty("denominator").GetInt64());
    }

    [Fact]
    public async Task Employee_is_forbidden_and_unknown_report_or_bad_filter_is_400()
    {
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "emp@example.com");
        using var forbidden = await employee.GetAsync(IncidentsPath);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        using var unknown = await admin.GetAsync("/api/v1/reports/nope");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        using var badEnum = await admin.GetAsync($"{IncidentsPath}?priority=nope");
        Assert.Equal(HttpStatusCode.BadRequest, badEnum.StatusCode);
        Assert.Contains(ErrorCodes.ReportsInvalidFilter, (await ApiClient.ReadFieldCodesAsync(badEnum))["priority"]);
    }

    [Theory]
    [InlineData("from=1999-12-31", "from")]
    [InlineData("to=2101-01-01", "to")]
    [InlineData("from=0001-01-01&to=9999-12-31", "from")]
    public async Task Days_outside_2000_to_2100_are_400_not_500(string query, string field)
    {
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");

        using var response = await admin.GetAsync($"{IncidentsPath}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.ReportsInvalidFilter, (await ApiClient.ReadFieldCodesAsync(response))[field]);
    }
}
