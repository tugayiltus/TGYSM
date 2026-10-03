using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Reports;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Reports;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>
/// Background exports (spec §7.3, ADR-0020): request → pending → runner → ready with the drill-down's rows; runners claim with a
/// row lease (ADR-0014) and build under the creator's current scope; download is creator-only, re-checks the creator and expires.
/// </summary>
public sealed class ExportJobTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const string ReportsPath = "/api/v1/reports";
    private const string ExportsPath = "/api/v1/reports/exports";
    private const string March = "from=2026-03-01&to=2026-03-31";
    private const string VolumeQuery = "report=incidents&metric=volume&" + March;
    private const string RecordsSheet = "Kayıtlar";
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string LeakedSecret = "hunter2";
    private const int ParallelJobs = 5;
    private const int Batch = 10;

    private static readonly DateTimeOffset Start = new(2026, 3, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.Clock.AdjustTime(Start); // AdjustTime, not SetUtcNow: a test before this one may have advanced the shared clock
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class NoAudit : IAuditLog
    {
        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null, object? newValues = null, string? reason = null, Guid? actorUserId = null)
        {
        }
    }

    /// <summary>The real Excel writer, counting how many files it was asked to write (thread-safe: two runners share it).</summary>
    private sealed class CountingWriter(IReportFileWriter inner) : IReportFileWriter
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task WriteAsync(ReportExportContent content, Stream output, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return inner.WriteAsync(content, output, cancellationToken);
        }
    }

    /// <summary>Writes a few bytes, then fails with a credential-shaped message, like a disk or library failure mid-file.</summary>
    private sealed class FailingWriter : IReportFileWriter
    {
        public async Task WriteAsync(ReportExportContent content, Stream output, CancellationToken cancellationToken)
        {
            await output.WriteAsync(new byte[] { 1, 2, 3 }, cancellationToken);
            await output.FlushAsync(cancellationToken);
            throw new IOException($"disk failed password={LeakedSecret}");
        }
    }

    /// <summary>Three March incidents in the desk team and one in the field team.</summary>
    private async Task<ReportWorld> SeedAsync()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var fx = new ReportFixture(world);
        await fx.IncidentAsync(T0);
        await fx.IncidentAsync(T0.AddDays(1));
        await fx.IncidentAsync(T0.AddDays(2));
        await fx.IncidentAsync(T0, teamId: world.FieldTeam.Id);
        return world;
    }

    private async Task<ApiClient> LoginAsync(string email)
    {
        var client = ApiClient.Create(factory);
        await client.LoginAsync(email);
        return client;
    }

    private static async Task<Guid> RequestAsync(ApiClient client, string query = VolumeQuery)
    {
        using var response = await client.PostAsync($"{ExportsPath}?{query}");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await client.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("pending", body.GetProperty("status").GetString());
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<int> RunAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RunExportJobsHandler>().HandleAsync(Batch, Lease, Ct);
    }

    private async Task<ExportJob> JobAsync(Guid id)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.ExportJobs.AsNoTracking().SingleAsync(j => j.Id == id, Ct);
    }

    private int StoredFileCount() =>
        Directory.Exists(factory.FilesRoot) ? Directory.GetFiles(factory.FilesRoot, "*", SearchOption.AllDirectories).Length : 0;

    private static string DownloadPath(Guid id) => $"{ExportsPath}/{id}/download";

    private async Task<Guid> InsertJobAsync(Guid creator, string metric = "volume")
    {
        await using var db = factory.Database.CreateDbContext();
        var filter = new ReportFilter { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 31) };
        var job = ExportJob.Create(ReportName.Incidents, metric, null, ReportDrillPart.Numerator, filter.ToJson(), creator, Start);
        db.ExportJobs.Add(job);
        await db.SaveChangesAsync(Ct);
        return job.Id;
    }

    [Fact]
    public async Task A_requested_job_is_pending_then_ready_with_the_drilldown_rows_and_downloads_once_ready()
    {
        var world = await SeedAsync();
        var admin = await LoginAsync(world.Admin.Email);

        var id = await RequestAsync(admin);
        Assert.Equal(ExportJobStatus.Pending, (await JobAsync(id)).Status);

        Assert.Equal(1, await RunAsync(factory.Services));

        using var drill = await admin.GetAsync($"{ReportsPath}/incidents/drilldown?metric=volume&{March}");
        var total = (await admin.ReadJsonAsync<JsonElement>(drill)).GetProperty("tickets").GetProperty("totalCount").GetInt32();
        var job = await JobAsync(id);
        Assert.Equal(ExportJobStatus.Ready, job.Status);
        Assert.Equal(4, total);
        Assert.Equal(total, job.RowCount);
        Assert.Equal(Start + ReportExportLimits.FileLifetime, job.ExpiresAt);

        using var list = await admin.GetAsync(ExportsPath);
        var item = (await admin.ReadJsonAsync<JsonElement>(list)).GetProperty("items").EnumerateArray().Single();
        Assert.Equal("ready", item.GetProperty("status").GetString());
        Assert.Equal("incidents", item.GetProperty("report").GetString());
        Assert.Equal(total, item.GetProperty("rowCount").GetInt32());

        using var download = await admin.GetAsync(DownloadPath(id));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(XlsxType, download.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("tgysm-incidents-volume-", download.Content.Headers.ContentDisposition!.FileNameStar ?? download.Content.Headers.ContentDisposition.FileName);
        using var workbook = new XLWorkbook(new MemoryStream(await download.Content.ReadAsByteArrayAsync(Ct)));
        Assert.Equal(total, workbook.Worksheet(RecordsSheet).LastRowUsed()!.RowNumber() - 1);

        await using var db = factory.Database.CreateDbContext();
        var requested = await db.AuditEvents.SingleAsync(a => a.Action == AuditActions.ReportExportRequested, Ct);
        Assert.Equal(id, requested.EntityId);
        Assert.Equal(AuditEntityTypes.ExportJob, requested.EntityType);
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ReportExportDownloaded && a.EntityId == id, Ct));
    }

    [Fact]
    public async Task A_job_under_a_live_lease_is_not_claimed_again()
    {
        var world = await SeedAsync();
        var id = await InsertJobAsync(world.Admin.Id);

        async Task<IReadOnlyList<ExportJob>> ClaimAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IExportJobClaimer>().ClaimAsync(Batch, Lease, Ct);
        }

        Assert.Equal(id, Assert.Single(await ClaimAsync()).Id);
        Assert.Empty(await ClaimAsync());
    }

    [Fact]
    public async Task Two_parallel_runners_process_each_of_five_jobs_exactly_once()
    {
        var world = await SeedAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < ParallelJobs; i++)
        {
            ids.Add(await InsertJobAsync(world.Admin.Id));
        }

        var counter = new CountingWriter(new ExcelReportWriter(new TestClock(), factory.Clock));
        await using var counted = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IReportFileWriter>(_ => counter)));

        var runs = await Task.WhenAll(Task.Run(() => RunAsync(counted.Services), Ct), Task.Run(() => RunAsync(counted.Services), Ct));

        Assert.Equal(ParallelJobs, runs.Sum());
        Assert.Equal(ParallelJobs, counter.Calls);
        await using var db = factory.Database.CreateDbContext();
        var jobs = await db.ExportJobs.AsNoTracking().Where(j => ids.Contains(j.Id)).ToListAsync(Ct);
        Assert.All(jobs, j => Assert.Equal(ExportJobStatus.Ready, j.Status));
        Assert.Equal(ParallelJobs, jobs.Select(j => j.FileKey).Distinct().Count());
    }

    [Fact]
    public async Task It_staff_removed_from_a_team_after_the_request_gets_a_file_of_the_reduced_scope()
    {
        var world = await SeedAsync();
        await using (var db = factory.Database.CreateDbContext())
        {
            db.TeamMemberships.Add(TeamMembership.Create(world.FieldTeam.Id, world.Desk.Id, Start));
            await db.SaveChangesAsync(Ct);
        }

        var desk = await LoginAsync(world.Desk.Email);
        var id = await RequestAsync(desk);
        await using (var db = factory.Database.CreateDbContext())
        {
            await db.TeamMemberships.Where(m => m.TeamId == world.FieldTeam.Id && m.UserId == world.Desk.Id).ExecuteDeleteAsync(Ct);
        }

        await RunAsync(factory.Services);

        var job = await JobAsync(id);
        Assert.Equal(ExportJobStatus.Ready, job.Status);
        Assert.Equal(3, job.RowCount); // the field team's ticket was in scope when asked, not when built
    }

    [Fact]
    public async Task A_demoted_creator_and_another_user_get_404()
    {
        var world = await SeedAsync();
        var desk = await LoginAsync(world.Desk.Email);
        var id = await RequestAsync(desk);
        await RunAsync(factory.Services);

        var other = await LoginAsync(world.Admin.Email);
        using (var response = await other.GetAsync(DownloadPath(id)))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using (var list = await other.GetAsync(ExportsPath))
        {
            Assert.Empty((await other.ReadJsonAsync<JsonElement>(list)).GetProperty("items").EnumerateArray());
        }

        await using (var db = factory.Database.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == world.Desk.Id, Ct);
            user.ChangeRole(UserRole.Employee, Start);
            await db.SaveChangesAsync(Ct);
        }

        var demoted = await LoginAsync(world.Desk.Email);
        using var gone = await demoted.GetAsync(DownloadPath(id));
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_creator_gets_404()
    {
        var world = await SeedAsync();
        var id = await RequestAsync(await LoginAsync(world.Desk.Email));
        await RunAsync(factory.Services);
        await using var db = factory.Database.CreateDbContext();
        var user = await db.Users.SingleAsync(u => u.Id == world.Desk.Id, Ct);
        user.Deactivate(Start);
        await db.SaveChangesAsync(Ct);

        var handler = new DownloadExportHandler(
            db, new FixedActor(world.Desk.Id), factory.Services.GetRequiredService<IFileStore>(), new NoAudit(), factory.Clock, new TestClock());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.HandleAsync(new DownloadExportQuery(id), Ct));
    }

    [Fact]
    public async Task Not_ready_is_409_failed_is_404_and_after_the_lifetime_it_is_410()
    {
        var world = await SeedAsync();
        var admin = await LoginAsync(world.Admin.Email);
        var id = await RequestAsync(admin);

        using (var early = await admin.GetAsync(DownloadPath(id)))
        {
            Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
            Assert.Equal(ErrorCodes.ReportsExportNotReady, await ApiClient.ReadCodeAsync(early));
        }

        await RunAsync(factory.Services);
        factory.Clock.Advance(ReportExportLimits.FileLifetime);
        var later = await LoginAsync(world.Admin.Email); // the first session ran out with the clock

        using (var expired = await later.GetAsync(DownloadPath(id)))
        {
            Assert.Equal(HttpStatusCode.Gone, expired.StatusCode);
            Assert.Equal(ErrorCodes.ReportsExportExpired, await ApiClient.ReadCodeAsync(expired));
        }

        using (var list = await later.GetAsync(ExportsPath))
        {
            var item = (await later.ReadJsonAsync<JsonElement>(list)).GetProperty("items").EnumerateArray().Single();
            Assert.Equal("expired", item.GetProperty("status").GetString());
        }

        var failed = await InsertJobAsync(world.Admin.Id, metric: "no_such_metric");
        await RunAsync(factory.Services);
        Assert.Equal(ExportJobStatus.Failed, (await JobAsync(failed)).Status);
        using var missing = await later.GetAsync(DownloadPath(failed));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task An_inactive_creator_fails_the_job_without_a_file()
    {
        var world = await SeedAsync();
        var id = await InsertJobAsync(world.Desk.Id);
        await using (var db = factory.Database.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == world.Desk.Id, Ct);
            user.Deactivate(Start);
            await db.SaveChangesAsync(Ct);
        }

        var files = StoredFileCount();
        Assert.Equal(1, await RunAsync(factory.Services));

        var job = await JobAsync(id);
        Assert.Equal(ExportJobStatus.Failed, job.Status);
        Assert.Equal(ErrorCodes.Forbidden, job.LastError);
        Assert.Null(job.FileKey);
        Assert.Equal(files, StoredFileCount());
    }

    [Fact]
    public async Task A_failure_mid_write_leaves_no_file_and_a_sanitized_error()
    {
        var world = await SeedAsync();
        var id = await InsertJobAsync(world.Admin.Id);
        await using var failing = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IReportFileWriter, FailingWriter>()));
        var files = StoredFileCount();

        Assert.Equal(1, await RunAsync(failing.Services));

        var job = await JobAsync(id);
        Assert.Equal(ExportJobStatus.Failed, job.Status);
        Assert.Null(job.FileKey);
        Assert.DoesNotContain(LeakedSecret, job.LastError);
        Assert.Contains(nameof(IOException), job.LastError);
        Assert.Equal(files, StoredFileCount());
    }

    [Fact]
    public async Task Above_the_job_limit_is_refused_up_front_and_a_job_that_grew_past_it_fails()
    {
        var world = await SeedAsync();
        await using (var db = factory.Database.CreateDbContext())
        {
            var template = await db.Tickets.Where(t => t.TeamId == world.DeskTeam.Id).FirstAsync(Ct);
            // ReportExportLimits.JobMaxRows copies of one March incident, inserted by SQL: EF would take minutes for this many rows.
            await db.Database.ExecuteSqlRawAsync(
                """
                DO $$
                DECLARE cols text;
                BEGIN
                  SELECT string_agg(quote_ident(column_name), ', ') INTO cols
                  FROM information_schema.columns WHERE table_name = 'tickets' AND column_name NOT IN ('id', 'number');
                  EXECUTE format('INSERT INTO tickets (id, number, %s) SELECT gen_random_uuid(), ''BULK-'' || g, %s FROM tickets CROSS JOIN generate_series(1, {0}) g WHERE id = ''{1}''', cols, cols);
                END $$;
                """.Replace("{0}", ReportExportLimits.JobMaxRows.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{1}", template.Id.ToString(), StringComparison.Ordinal),
                Ct);
        }

        var admin = await LoginAsync(world.Admin.Email);
        using (var refused = await admin.PostAsync($"{ExportsPath}?{VolumeQuery}"))
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal(ErrorCodes.ReportsExportTooLarge, await ApiClient.ReadCodeAsync(refused));
        }

        await using (var db = factory.Database.CreateDbContext())
        {
            Assert.False(await db.ExportJobs.AnyAsync(Ct));
        }

        var grown = await InsertJobAsync(world.Admin.Id);
        await RunAsync(factory.Services);

        var job = await JobAsync(grown);
        Assert.Equal(ExportJobStatus.Failed, job.Status);
        Assert.Equal(ErrorCodes.ReportsExportTooLarge, job.LastError);
    }

    [Fact]
    public async Task Bad_requests_and_employees_are_refused()
    {
        var world = await SeedAsync();
        var admin = await LoginAsync(world.Admin.Email);
        using (var unknown = await admin.PostAsync($"{ExportsPath}?report=incidents&metric=nope"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        }

        var employee = await LoginAsync(world.Employee.Email);
        using var forbidden = await employee.PostAsync($"{ExportsPath}?{VolumeQuery}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
