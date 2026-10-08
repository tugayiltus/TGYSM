using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Operations;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Operations;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Files;

namespace Tgysm.Api.IntegrationTests.Operations;

/// <summary><see cref="CleanupHandler"/> (ADR-0014): only rows past the retention cutoff are removed.</summary>
public sealed class CleanupTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private static readonly DateTimeOffset Today = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Old = CleanupHandler.Retention + TimeSpan.FromDays(1);
    private static readonly TimeSpan Recent = CleanupHandler.Retention - TimeSpan.FromDays(1);
    private const string TokenHashPrefix = "hash-";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.Clock.SetUtcNow(Today);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<CleanupResult> RunAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CleanupHandler>().HandleAsync(Ct);
    }

    private static async Task<CleanupResult> RunAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CleanupHandler>().HandleAsync(Ct);
    }

    /// <summary>A file store whose delete of one key fails, as a locked or permission-denied file would.</summary>
    private sealed class FailingDeleteStore(IFileStore inner, string brokenKey) : IFileStore
    {
        public Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => inner.SaveAsync(content, cancellationToken);

        public Task<string> SaveAsync(Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken) => inner.SaveAsync(write, cancellationToken);

        public Stream? OpenRead(string storageKey) => inner.OpenRead(storageKey);

        public void Delete(string storageKey)
        {
            if (storageKey == brokenKey)
            {
                throw new IOException("The file is locked.");
            }

            inner.Delete(storageKey);
        }
    }

    private static ExportJob ReadyJob(Guid creator, string key, DateTimeOffset finished)
    {
        var job = ExportJob.Create(ReportName.Incidents, "volume", null, ReportDrillPart.Numerator, "{}", creator, finished);
        job.Start(finished);
        job.Complete(key, 1, finished);
        return job;
    }

    private static string Payload(string email) => JsonSerializer.Serialize(new Dictionary<string, string> { [OutboxEvent.RequestedEmailKey] = email });

    [Fact]
    public async Task Only_password_tokens_expired_past_the_cutoff_are_removed()
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, "tok@example.com");
        await using (var db = factory.Database.CreateDbContext())
        {
            // Issued at (now - age - lifetime) so that ExpiresAt = now - age.
            db.PasswordTokens.Add(PasswordToken.Issue(user.Id, PasswordTokenPurpose.Reset, TokenHashPrefix + "old", Today - Old - PasswordToken.ResetLifetime));
            db.PasswordTokens.Add(PasswordToken.Issue(user.Id, PasswordTokenPurpose.Reset, TokenHashPrefix + "recent", Today - Recent - PasswordToken.ResetLifetime));
            db.PasswordTokens.Add(PasswordToken.Issue(user.Id, PasswordTokenPurpose.Activation, TokenHashPrefix + "live", Today));
            await db.SaveChangesAsync(Ct);
        }

        var result = await RunAsync();

        Assert.Equal(1, result.PasswordTokens);
        await using var check = factory.Database.CreateDbContext();
        var hashes = await check.PasswordTokens.Select(t => t.TokenHash).ToListAsync(Ct);
        Assert.Equal([TokenHashPrefix + "recent", TokenHashPrefix + "live"], hashes.Order().Reverse().ToList());
    }

    [Fact]
    public async Task Old_finished_events_go_with_their_deliveries_and_the_rest_is_kept()
    {
        var recipient = new Recipient(Guid.NewGuid(), "r@example.com", "R");
        Guid oldDone, oldSkipped, oldPendingDelivery, oldPendingEvent, recentDone, oldFailed;
        await using (var db = factory.Database.CreateDbContext())
        {
            OutboxEvent Make(DateTimeOffset at, Action<OutboxEvent> shape)
            {
                var e = OutboxEvent.Create(NotificationEventTypes.TicketCreated, null, null, "{}", null, at);
                shape(e);
                db.OutboxEvents.Add(e);
                return e;
            }

            var done = Make(Today - Old, e => e.MarkDispatched(Today - Old));
            var d1 = EmailDelivery.Create(done.Id, recipient, NotificationEventTypes.TicketCreated, "s", "b", Today - Old);
            d1.MarkSent(Today - Old);
            db.EmailDeliveries.Add(d1);
            oldDone = done.Id;

            oldSkipped = Make(Today - Old, e => e.MarkSkipped(Today - Old)).Id;

            var withPending = Make(Today - Old, e => e.MarkDispatched(Today - Old));
            db.EmailDeliveries.Add(EmailDelivery.Create(withPending.Id, recipient, NotificationEventTypes.TicketCreated, "s", "b", Today - Old));
            oldPendingDelivery = withPending.Id;

            oldPendingEvent = Make(Today - Old, _ => { }).Id;
            recentDone = Make(Today - Recent, e => e.MarkDispatched(Today - Recent)).Id;
            oldFailed = Make(Today - Old, e =>
            {
                for (var i = 0; i < OutboxEvent.MaxAttempts; i++)
                {
                    e.RecordFailure("x", Today - Old);
                }
            }).Id;
            await db.SaveChangesAsync(Ct);
        }

        var result = await RunAsync();

        Assert.Equal(2, result.OutboxEvents);
        await using var check = factory.Database.CreateDbContext();
        var ids = await check.OutboxEvents.Select(e => e.Id).ToListAsync(Ct);
        Assert.DoesNotContain(oldDone, ids);
        Assert.DoesNotContain(oldSkipped, ids);
        Assert.Equivalent(new[] { oldPendingDelivery, oldPendingEvent, recentDone, oldFailed }, ids);
        Assert.Equal(1, await check.EmailDeliveries.CountAsync(Ct));
    }

    [Fact]
    public async Task Old_password_reset_requests_are_removed_so_the_email_does_not_linger()
    {
        await using (var db = factory.Database.CreateDbContext())
        {
            var old = OutboxEvent.Create(InternalEventTypes.PasswordResetRequested, null, null, Payload("a@example.com"), null, Today - Old);
            old.MarkSkipped(Today - Old);
            var recent = OutboxEvent.Create(InternalEventTypes.PasswordResetRequested, null, null, Payload("b@example.com"), null, Today - Recent);
            recent.MarkSkipped(Today - Recent);
            db.OutboxEvents.AddRange(old, recent);
            await db.SaveChangesAsync(Ct);
        }

        var result = await RunAsync();

        Assert.Equal(1, result.OutboxEvents);
        await using var check = factory.Database.CreateDbContext();
        var left = await check.OutboxEvents.SingleAsync(Ct);
        Assert.Contains("b@example.com", left.Payload);
    }

    [Fact]
    public async Task Only_heartbeats_last_run_past_the_cutoff_are_removed()
    {
        await using (var db = factory.Database.CreateDbContext())
        {
            db.WorkerHeartbeats.Add(WorkerHeartbeat.Create("J", "old:1", Today - Old, null));
            db.WorkerHeartbeats.Add(WorkerHeartbeat.Create("J", "new:2", Today - Recent, null));
            await db.SaveChangesAsync(Ct);
        }

        var result = await RunAsync();

        Assert.Equal(1, result.Heartbeats);
        await using var check = factory.Database.CreateDbContext();
        Assert.Equal("new:2", (await check.WorkerHeartbeats.SingleAsync(Ct)).Instance);
    }

    [Fact]
    public async Task Expired_export_files_are_deleted_and_their_jobs_marked_expired_while_live_ones_stay()
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.Admin, "exports@example.com");
        var files = factory.Services.GetRequiredService<IFileStore>();
        var expiredKey = await files.SaveAsync(new byte[] { 1 }, Ct);
        var liveKey = await files.SaveAsync(new byte[] { 2 }, Ct);
        ExportJob Ready(string key, DateTimeOffset finished)
        {
            var job = ExportJob.Create(ReportName.Incidents, "volume", null, ReportDrillPart.Numerator, "{}", user.Id, finished);
            job.Start(finished);
            job.Complete(key, 1, finished);
            return job;
        }

        var expired = Ready(expiredKey, Today - ReportExportLimits.FileLifetime); // expires exactly now
        var live = Ready(liveKey, Today - TimeSpan.FromHours(1));
        var failed = ExportJob.Create(ReportName.Incidents, "volume", null, ReportDrillPart.Numerator, "{}", user.Id, Today - Old);
        failed.Fail("x", Today - Old);
        await using (var db = factory.Database.CreateDbContext())
        {
            db.ExportJobs.AddRange(expired, live, failed);
            await db.SaveChangesAsync(Ct);
        }

        var result = await RunAsync();

        Assert.Equal(1, result.ExportFiles);
        Assert.Null(files.OpenRead(expiredKey));
        using (var stillThere = files.OpenRead(liveKey))
        {
            Assert.NotNull(stillThere);
        }

        await using var check = factory.Database.CreateDbContext();
        var jobs = await check.ExportJobs.AsNoTracking().ToDictionaryAsync(j => j.Id, Ct);
        Assert.Equal(ExportJobStatus.Expired, jobs[expired.Id].Status);
        Assert.Null(jobs[expired.Id].FileKey);
        Assert.Equal(ExportJobStatus.Ready, jobs[live.Id].Status);
        Assert.Equal(ExportJobStatus.Failed, jobs[failed.Id].Status);
    }

    [Fact]
    public async Task A_file_that_cannot_be_deleted_keeps_its_job_ready_and_the_other_jobs_still_expire()
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.Admin, "exports@example.com");
        var files = factory.Services.GetRequiredService<IFileStore>();
        var brokenKey = await files.SaveAsync(new byte[] { 1 }, Ct);
        var goodKey = await files.SaveAsync(new byte[] { 2 }, Ct);
        var finished = Today - ReportExportLimits.FileLifetime;
        var broken = ReadyJob(user.Id, brokenKey, finished);
        var good = ReadyJob(user.Id, goodKey, finished);
        await using (var db = factory.Database.CreateDbContext())
        {
            db.ExportJobs.AddRange(broken, good);
            await db.SaveChangesAsync(Ct);
        }

        await using var failing = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IFileStore>(provider => new FailingDeleteStore(provider.GetRequiredService<LocalFileStore>(), brokenKey))));
        var result = await RunAsync(failing.Services);

        Assert.Equal(1, result.ExportFiles);
        Assert.Null(files.OpenRead(goodKey));
        using (var kept = files.OpenRead(brokenKey))
        {
            Assert.NotNull(kept);
        }

        await using var check = factory.Database.CreateDbContext();
        var jobs = await check.ExportJobs.AsNoTracking().ToDictionaryAsync(j => j.Id, Ct);
        Assert.Equal(ExportJobStatus.Expired, jobs[good.Id].Status);
        Assert.Equal(ExportJobStatus.Ready, jobs[broken.Id].Status);
        Assert.Equal(brokenKey, jobs[broken.Id].FileKey);
    }

    [Fact]
    public async Task More_expired_exports_than_one_batch_all_expire_in_one_run()
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.Admin, "exports@example.com");
        var finished = Today - ReportExportLimits.FileLifetime;
        var count = CleanupHandler.ExportExpiryBatchSize + 1;
        await using (var db = factory.Database.CreateDbContext())
        {
            // Keys without a file: deleting a missing file is a no-op, which is all this test needs.
            db.ExportJobs.AddRange(Enumerable.Range(0, count).Select(_ => ReadyJob(user.Id, Guid.NewGuid().ToString("N"), finished)));
            await db.SaveChangesAsync(Ct);
        }

        var result = await RunAsync();

        Assert.Equal(count, result.ExportFiles);
        await using var check = factory.Database.CreateDbContext();
        Assert.False(await check.ExportJobs.AnyAsync(j => j.Status == ExportJobStatus.Ready, Ct));
    }
}
