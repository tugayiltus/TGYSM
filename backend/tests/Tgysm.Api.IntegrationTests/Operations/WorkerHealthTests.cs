using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Operations;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Operations;

[Collection(ApiCollection.Name)]
public sealed class WorkerHealthTests(ApiFactory factory) : IAsyncLifetime
{
    private const string WorkersPath = "/api/v1/admin/system/workers";
    private const string HealthPath = "/api/v1/health";
    private const string ConfiguredJob = "OutboxDispatcher";
    private const string UnconfiguredJob = "UnknownJob";
    private const string Instance = "host:1";

    /// <summary>Test host config sets the job interval to 30 s; 3 x 30 s = 90 s is the staleness limit.</summary>
    private static readonly TimeSpan WithinLimit = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BeyondLimit = TimeSpan.FromSeconds(120);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task SeedAsync(string job, TimeSpan age, string? error = null, string instance = Instance)
    {
        await using var db = factory.Database.CreateDbContext();
        db.WorkerHeartbeats.Add(WorkerHeartbeat.Create(job, instance, DateTimeOffset.UtcNow - age, error));
        await db.SaveChangesAsync(Ct);
    }

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private async Task<JsonElement> GetAsync(ApiClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Fresh_heartbeat_is_ok_and_not_stale()
    {
        await SeedAsync(ConfiguredJob, WithinLimit);
        var admin = await AdminAsync();

        var result = await GetAsync(admin, WorkersPath);

        Assert.Equal("ok", result.GetProperty("status").GetString());
        var job = Assert.Single(result.GetProperty("jobs").EnumerateArray());
        Assert.Equal(ConfiguredJob, job.GetProperty("job").GetString());
        Assert.Equal(30, job.GetProperty("intervalSeconds").GetInt32());
        Assert.False(job.GetProperty("stale").GetBoolean());
    }

    [Fact]
    public async Task Heartbeat_older_than_three_intervals_is_stale_and_degrades_status()
    {
        await SeedAsync(ConfiguredJob, BeyondLimit, "boom");
        var admin = await AdminAsync();

        var result = await GetAsync(admin, WorkersPath);

        Assert.Equal("degraded", result.GetProperty("status").GetString());
        var job = Assert.Single(result.GetProperty("jobs").EnumerateArray());
        Assert.True(job.GetProperty("stale").GetBoolean());
        Assert.Equal("boom", job.GetProperty("lastError").GetString());
    }

    [Fact]
    public async Task Only_the_newest_heartbeat_of_a_job_decides_after_a_worker_restart()
    {
        await SeedAsync(ConfiguredJob, BeyondLimit, instance: "host:old");
        await SeedAsync(ConfiguredJob, WithinLimit, instance: "host:new");
        var admin = await AdminAsync();
        var anonymous = ApiClient.Create(factory);

        var workers = await GetAsync(admin, WorkersPath);
        var health = await GetAsync(anonymous, HealthPath);

        Assert.Equal("ok", workers.GetProperty("status").GetString());
        Assert.Equal("ok", health.GetProperty("status").GetString());
        Assert.All(workers.GetProperty("jobs").EnumerateArray(), j => Assert.False(j.GetProperty("stale").GetBoolean()));
        Assert.Equal(2, workers.GetProperty("jobs").GetArrayLength());
    }

    [Fact]
    public async Task Job_without_configured_interval_uses_the_default()
    {
        await SeedAsync(UnconfiguredJob, WithinLimit);
        var admin = await AdminAsync();

        var result = await GetAsync(admin, WorkersPath);

        var job = Assert.Single(result.GetProperty("jobs").EnumerateArray());
        Assert.Equal(60, job.GetProperty("intervalSeconds").GetInt32());
        Assert.False(job.GetProperty("stale").GetBoolean());
    }

    [Fact]
    public async Task Health_endpoint_reports_degraded_when_a_job_is_stale()
    {
        await SeedAsync(ConfiguredJob, BeyondLimit);
        var anonymous = ApiClient.Create(factory);

        var result = await GetAsync(anonymous, HealthPath);

        Assert.Equal("degraded", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_endpoint_is_ok_without_heartbeats()
    {
        var anonymous = ApiClient.Create(factory);

        var result = await GetAsync(anonymous, HealthPath);

        Assert.Equal("ok", result.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Workers_endpoint_requires_admin()
    {
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "emp@example.com");

        using var response = await employee.GetAsync(WorkersPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
