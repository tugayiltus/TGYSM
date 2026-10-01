using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Sla;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Sla;

[Collection(ApiCollection.Name)]
public sealed class SlaAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SlaPath = "/api/v1/admin/sla";
    private const string CalendarPath = SlaPath + "/calendar";
    private const string HolidaysPath = SlaPath + "/holidays";
    private const string PolicyPath = SlaPath + "/policy";
    private const string RequestTypesPath = "/api/v1/admin/request-types";
    private const string SeedTimeZone = "Europe/Istanbul";
    private const int SeededWeekdays = 5;
    private const int MatrixSize = 16;
    private const int NewTargetMinutes = 20;
    private const int SeedWarningPercent = 75;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static object Hours(string weekday, string startsAt, string endsAt) => new { weekday, startsAt, endsAt };

    private static object[] WorkWeek() =>
        new[] { "monday", "tuesday", "wednesday", "thursday", "friday" }.Select(d => Hours(d, "08:00:00", "17:00:00")).ToArray();

    private async Task<JsonElement> GetCalendarAsync(ApiClient admin)
    {
        using var response = await admin.GetAsync(CalendarPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    private static async Task<JsonElement> GetPolicyAsync(ApiClient admin)
    {
        using var response = await admin.GetAsync(PolicyPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    private static List<object> FullTargets(int incidentP1ResolutionMinutes = NewTargetMinutes) =>
        (from kind in new[] { "incident", "request" }
         from priority in new[] { "p1", "p2", "p3", "p4" }
         from metric in new[] { "first_response", "resolution" }
         select (object)new
         {
             kind,
             priority,
             requestTypeId = (Guid?)null,
             metric,
             targetMinutes = kind == "incident" && priority == "p1" && metric == "resolution" ? incidentP1ResolutionMinutes : NewTargetMinutes,
             warningPercent = SeedWarningPercent,
         }).ToList();

    private static object[] PauseRules() => [new { holdReason = "user", pausesFirstResponse = false, pausesResolution = true }];

    private static object PolicyBody(IEnumerable<object>? targets = null) => new { targets = targets ?? FullTargets(), pauseRules = PauseRules() };

    [Fact]
    public async Task Seed_has_the_istanbul_work_week_and_the_first_policy_version()
    {
        var admin = await AdminAsync();

        var calendar = await GetCalendarAsync(admin);
        var policy = await GetPolicyAsync(admin);

        Assert.Equal(SeedTimeZone, calendar.GetProperty("timeZone").GetString());
        var hours = calendar.GetProperty("hours").EnumerateArray().ToList();
        Assert.Equal(SeededWeekdays, hours.Count);
        Assert.Equal(["monday", "tuesday", "wednesday", "thursday", "friday"], hours.Select(h => h.GetProperty("weekday").GetString()));
        Assert.All(hours, h =>
        {
            Assert.Equal(new TimeOnly(9, 0), TimeOnly.Parse(h.GetProperty("startsAt").GetString()!));
            Assert.Equal(new TimeOnly(18, 0), TimeOnly.Parse(h.GetProperty("endsAt").GetString()!));
        });

        Assert.Equal(1, policy.GetProperty("number").GetInt32());
        var targets = policy.GetProperty("targets").EnumerateArray().ToList();
        Assert.Equal(MatrixSize, targets.Count);
        int Minutes(string kind, string priority, string metric) => targets.Single(t =>
            t.GetProperty("kind").GetString() == kind && t.GetProperty("priority").GetString() == priority && t.GetProperty("metric").GetString() == metric)
            .GetProperty("targetMinutes").GetInt32();
        foreach (var kind in new[] { "incident", "request" })
        {
            Assert.Equal(30, Minutes(kind, "p1", "first_response"));
            Assert.Equal(240, Minutes(kind, "p1", "resolution"));
            Assert.Equal(60, Minutes(kind, "p2", "first_response"));
            Assert.Equal(480, Minutes(kind, "p2", "resolution"));
            Assert.Equal(240, Minutes(kind, "p3", "first_response"));
            Assert.Equal(3 * 540, Minutes(kind, "p3", "resolution"));
            Assert.Equal(480, Minutes(kind, "p4", "first_response"));
            Assert.Equal(5 * 540, Minutes(kind, "p4", "resolution"));
        }

        Assert.All(targets, t => Assert.Equal(SeedWarningPercent, t.GetProperty("warningPercent").GetInt32()));
        var rules = policy.GetProperty("pauseRules").EnumerateArray().ToList();
        Assert.Equal(["manager_approval", "user"], rules.Select(r => r.GetProperty("holdReason").GetString()).Order(StringComparer.Ordinal));
        Assert.All(rules, r =>
        {
            Assert.False(r.GetProperty("pausesFirstResponse").GetBoolean());
            Assert.True(r.GetProperty("pausesResolution").GetBoolean());
        });
    }

    [Fact]
    public async Task Calendar_update_saves_zone_and_hours_and_is_audited_and_feeds_the_provider()
    {
        var admin = await AdminAsync();
        var calendar = await GetCalendarAsync(admin);

        using var response = await admin.PutAsync(CalendarPath, new
        {
            version = calendar.GetProperty("version").GetUInt32(),
            timeZone = "America/New_York",
            hours = WorkWeek().Append(Hours("saturday", "10:00:00", "14:00:00")),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await admin.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("America/New_York", updated.GetProperty("timeZone").GetString());
        Assert.Equal(6, updated.GetProperty("hours").GetArrayLength());

        await using var scope = factory.Services.CreateAsyncScope();
        var rules = await scope.ServiceProvider.GetRequiredService<CalendarProvider>().GetRulesAsync(Ct);
        Assert.Equal("America/New_York", rules.Zone.Id);
        Assert.Equal(new WorkingRange(new TimeOnly(8, 0), new TimeOnly(17, 0)), rules.Hours[DayOfWeek.Monday]);
        Assert.Equal(new WorkingRange(new TimeOnly(10, 0), new TimeOnly(14, 0)), rules.Hours[DayOfWeek.Saturday]);
        Assert.False(rules.Hours.ContainsKey(DayOfWeek.Sunday));

        await using var db = factory.Database.CreateDbContext();
        Assert.Contains(AuditActions.SlaCalendarUpdated, await db.AuditEvents.Select(a => a.Action).ToListAsync(Ct));
    }

    [Fact]
    public async Task Calendar_update_can_close_a_weekday()
    {
        var admin = await AdminAsync();
        var calendar = await GetCalendarAsync(admin);

        using var response = await admin.PutAsync(CalendarPath, new
        {
            version = calendar.GetProperty("version").GetUInt32(),
            timeZone = SeedTimeZone,
            hours = WorkWeek().Take(4),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, (await GetCalendarAsync(admin)).GetProperty("hours").GetArrayLength());
    }

    [Fact]
    public async Task Calendar_update_rejects_a_non_IANA_time_zone()
    {
        var admin = await AdminAsync();
        var calendar = await GetCalendarAsync(admin);

        using var response = await admin.PutAsync(CalendarPath, new
        {
            version = calendar.GetProperty("version").GetUInt32(),
            timeZone = "Turkey Standard Time",
            hours = WorkWeek(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([ErrorCodes.SlaInvalidTimeZone], (await ApiClient.ReadFieldCodesAsync(response))["timeZone"]);
    }

    [Fact]
    public async Task Calendar_update_rejects_inverted_and_overlapping_hours()
    {
        var admin = await AdminAsync();
        var calendar = await GetCalendarAsync(admin);
        var version = calendar.GetProperty("version").GetUInt32();

        using var inverted = await admin.PutAsync(CalendarPath, new { version, timeZone = SeedTimeZone, hours = new[] { Hours("monday", "18:00:00", "09:00:00") } });
        using var overlapping = await admin.PutAsync(CalendarPath, new
        {
            version,
            timeZone = SeedTimeZone,
            hours = new[] { Hours("monday", "09:00:00", "13:00:00"), Hours("monday", "12:00:00", "18:00:00") },
        });

        Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);
        Assert.Equal([ErrorCodes.SlaInvalidHours], (await ApiClient.ReadFieldCodesAsync(inverted))["hours"]);
        Assert.Equal(HttpStatusCode.BadRequest, overlapping.StatusCode);
        Assert.Equal([ErrorCodes.SlaInvalidHours], (await ApiClient.ReadFieldCodesAsync(overlapping))["hours"]);
        Assert.Equal(SeededWeekdays, (await GetCalendarAsync(admin)).GetProperty("hours").GetArrayLength());
    }

    [Fact]
    public async Task Calendar_update_rejects_a_week_with_no_working_day()
    {
        var admin = await AdminAsync();
        var calendar = await GetCalendarAsync(admin);

        using var response = await admin.PutAsync(CalendarPath, new
        {
            version = calendar.GetProperty("version").GetUInt32(),
            timeZone = SeedTimeZone,
            hours = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([ErrorCodes.SlaCalendarEmpty], (await ApiClient.ReadFieldCodesAsync(response))["hours"]);
    }

    [Fact]
    public async Task Calendar_update_with_a_stale_version_conflicts()
    {
        var admin = await AdminAsync();
        var calendar = await GetCalendarAsync(admin);
        var body = new { version = calendar.GetProperty("version").GetUInt32(), timeZone = SeedTimeZone, hours = WorkWeek() };

        using var first = await admin.PutAsync(CalendarPath, body);
        using var second = await admin.PutAsync(CalendarPath, body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task Holiday_is_added_listed_provided_removed_and_audited()
    {
        var admin = await AdminAsync();

        using var add = await admin.PostAsync(HolidaysPath, new { date = "2026-10-29", name = "Cumhuriyet Bayramı" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var holiday = await admin.ReadJsonAsync<JsonElement>(add);

        using var list = await admin.GetAsync(HolidaysPath);
        var listed = (await admin.ReadJsonAsync<JsonElement>(list)).EnumerateArray().ToList();
        Assert.Equal("2026-10-29", Assert.Single(listed).GetProperty("date").GetString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var rules = await scope.ServiceProvider.GetRequiredService<CalendarProvider>().GetRulesAsync(Ct);
            Assert.Contains(new DateOnly(2026, 10, 29), rules.Holidays);
        }

        using var remove = await admin.DeleteAsync($"{HolidaysPath}/{holiday.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        using var after = await admin.GetAsync(HolidaysPath);
        Assert.Empty((await admin.ReadJsonAsync<JsonElement>(after)).EnumerateArray());

        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents.Select(a => a.Action).ToListAsync(Ct);
        Assert.Contains(AuditActions.SlaHolidayAdded, actions);
        Assert.Contains(AuditActions.SlaHolidayRemoved, actions);
    }

    [Fact]
    public async Task Adding_the_same_holiday_date_twice_conflicts()
    {
        var admin = await AdminAsync();
        using var first = await admin.PostAsync(HolidaysPath, new { date = "2026-05-01", name = "Emek ve Dayanışma Günü" });

        using var second = await admin.PostAsync(HolidaysPath, new { date = "2026-05-01", name = "Başka ad" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.SlaHolidayExists, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task Holiday_needs_a_name_and_removing_an_unknown_one_is_404()
    {
        var admin = await AdminAsync();

        using var nameless = await admin.PostAsync(HolidaysPath, new { date = "2026-05-01", name = " " });
        using var missing = await admin.DeleteAsync($"{HolidaysPath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, nameless.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task New_policy_version_keeps_the_old_rows_and_becomes_the_only_current_one()
    {
        var admin = await AdminAsync();
        var before = await GetPolicyAsync(admin);

        using var response = await admin.PostAsync(PolicyPath, PolicyBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await admin.ReadJsonAsync<JsonElement>(response);
        Assert.Equal(2, created.GetProperty("number").GetInt32());
        Assert.NotEqual(before.GetProperty("versionId").GetGuid(), created.GetProperty("versionId").GetGuid());
        Assert.Equal(created.GetProperty("versionId").GetGuid(), (await GetPolicyAsync(admin)).GetProperty("versionId").GetGuid());

        await using var db = factory.Database.CreateDbContext();
        var versions = await db.SlaPolicyVersions.OrderBy(v => v.Number).ToListAsync(Ct);
        Assert.Equal([1, 2], versions.Select(v => v.Number));
        Assert.Equal([false, true], versions.Select(v => v.IsCurrent));
        var oldTargets = await db.SlaTargets.Where(t => t.PolicyVersionId == versions[0].Id).ToListAsync(Ct);
        Assert.Equal(MatrixSize, oldTargets.Count);
        Assert.Equal(240, oldTargets.Single(t => t is { Kind: TicketKind.Incident, Priority: Priority.P1, Metric: SlaMetric.Resolution }).TargetMinutes);
        Assert.Equal(2, await db.SlaPauseRules.CountAsync(r => r.PolicyVersionId == versions[0].Id, Ct));
        Assert.Contains(AuditActions.SlaPolicyVersioned, await db.AuditEvents.Select(a => a.Action).ToListAsync(Ct));

        await using var scope = factory.Services.CreateAsyncScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<PolicyProvider>().GetCurrentAsync(Ct);
        Assert.Equal(versions[1].Id, snapshot.VersionId);
        Assert.Equal(NewTargetMinutes, snapshot.TargetFor(TicketKind.Incident, Priority.P1, null, SlaMetric.Resolution).TargetMinutes);
        Assert.Equal([HoldReason.User], snapshot.PauseRules.Select(r => r.HoldReason));
    }

    [Fact]
    public async Task Policy_version_can_carry_a_request_type_override()
    {
        var admin = await AdminAsync();
        using var typeResponse = await admin.PostAsync(RequestTypesPath, new { name = "VPN erişimi", requiresApproval = false });
        var requestTypeId = (await admin.ReadJsonAsync<JsonElement>(typeResponse)).GetProperty("id").GetGuid();
        var targets = FullTargets();
        targets.Add(new { kind = "request", priority = "p3", requestTypeId = (Guid?)requestTypeId, metric = "resolution", targetMinutes = 100, warningPercent = SeedWarningPercent });

        using var response = await admin.PostAsync(PolicyPath, PolicyBody(targets));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<PolicyProvider>().GetCurrentAsync(Ct);
        Assert.Equal(100, snapshot.TargetFor(TicketKind.Request, Priority.P3, requestTypeId, SlaMetric.Resolution).TargetMinutes);
        Assert.Equal(NewTargetMinutes, snapshot.TargetFor(TicketKind.Request, Priority.P3, null, SlaMetric.Resolution).TargetMinutes);
    }

    [Fact]
    public async Task Policy_version_without_a_full_target_matrix_is_rejected_and_nothing_changes()
    {
        var admin = await AdminAsync();
        var targets = FullTargets().Skip(1).ToList();

        using var response = await admin.PostAsync(PolicyPath, PolicyBody(targets));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([ErrorCodes.SlaTargetMissing], (await ApiClient.ReadFieldCodesAsync(response))["targets"]);
        Assert.Equal(1, (await GetPolicyAsync(admin)).GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task Policy_version_rejects_an_unknown_request_type_and_out_of_range_numbers()
    {
        var admin = await AdminAsync();
        var unknownType = FullTargets();
        unknownType.Add(new { kind = "request", priority = "p1", requestTypeId = (Guid?)Guid.NewGuid(), metric = "resolution", targetMinutes = 10, warningPercent = SeedWarningPercent });
        var zeroMinutes = FullTargets();
        zeroMinutes[0] = new { kind = "incident", priority = "p1", requestTypeId = (Guid?)null, metric = "first_response", targetMinutes = 0, warningPercent = SeedWarningPercent };

        using var unknown = await admin.PostAsync(PolicyPath, PolicyBody(unknownType));
        using var zero = await admin.PostAsync(PolicyPath, PolicyBody(zeroMinutes));

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(1, (await GetPolicyAsync(admin)).GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task Two_parallel_versioning_calls_leave_exactly_one_current_version()
    {
        var admin = await AdminAsync();
        var second = (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin2@example.com")).Client;

        var responses = await Task.WhenAll(admin.PostAsync(PolicyPath, PolicyBody()), second.PostAsync(PolicyPath, PolicyBody()));

        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var db = factory.Database.CreateDbContext();
        var versions = await db.SlaPolicyVersions.OrderBy(v => v.Number).ToListAsync(Ct);
        Assert.Equal([1, 2, 3], versions.Select(v => v.Number));
        Assert.Equal(1, versions.Count(v => v.IsCurrent));
        Assert.True(versions[^1].IsCurrent);
    }

    [Fact]
    public async Task It_staff_cannot_use_the_sla_admin_endpoints()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var calendar = await it.GetAsync(CalendarPath);
        using var holidays = await it.GetAsync(HolidaysPath);
        using var policy = await it.GetAsync(PolicyPath);
        using var newVersion = await it.PostAsync(PolicyPath, PolicyBody());
        using var addHoliday = await it.PostAsync(HolidaysPath, new { date = "2026-05-01", name = "x" });

        Assert.All(new[] { calendar, holidays, policy, newVersion, addHoliday }, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }
}
