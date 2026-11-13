using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(DatabaseCollection.Name)]
public sealed class ChangePersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string CheckViolation = "23514";
    private const int ParallelRequests = 4;
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly string Fingerprint = new('a', ChangeApproval.FingerprintLength);
    private User _author = null!;
    private Team _team = null!;
    private ChangeRequest _change = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        _author = User.Create("Ali", "Demir", "ali@example.com", UserRole.Admin, null, null, "hash", Now);
        _team = Team.Create("Ağ");
        var draft = new ChangeDraft("Anahtar güncelle", null, "Ağ", Level.Medium, Level.Low, null, null, null, null, null);
        _change = ChangeRequest.Create("CHG-2026-000001", draft, _team.Id, _author.Id, _author.Id, Now);
        await using var db = database.CreateDbContext();
        db.Users.Add(_author);
        db.Teams.Add(_team);
        db.ChangeRequests.Add(_change);
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Change_round_trips_with_events_and_a_version()
    {
        await using (var db = database.CreateDbContext())
        {
            db.ChangeEvents.Add(ChangeEvent.Record(_change.Id, ChangeEventKind.Created, _author.Id, "{}", Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var loaded = await read.ChangeRequests.SingleAsync(c => c.Id == _change.Id, Ct);
        Assert.Equal(("CHG-2026-000001", ChangeStatus.Draft, Level.Medium, Level.Low), (loaded.Number, loaded.Status, loaded.Risk, loaded.Impact));
        Assert.NotEqual(0u, loaded.Version);
        Assert.Equal(1, await read.ChangeEvents.CountAsync(e => e.ChangeId == _change.Id, Ct));
    }

    [Fact]
    public async Task Change_number_is_unique()
    {
        var draft = new ChangeDraft("Başka", null, null, Level.Low, Level.Low, null, null, null, null, null);
        await using var db = database.CreateDbContext();
        db.ChangeRequests.Add(ChangeRequest.Create("CHG-2026-000001", draft, _team.Id, _author.Id, _author.Id, Now));
        await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Parallel_requests_for_one_change_leave_one_pending_approval()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, ParallelRequests).Select(async _ =>
        {
            await using var db = database.CreateDbContext();
            db.ChangeApprovals.Add(ChangeApproval.Request(_change.Id, Fingerprint, Now));
            try
            {
                await db.SaveChangesAsync(Ct);
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }));

        Assert.Equal(1, results.Count(saved => saved));
        await using var read = database.CreateDbContext();
        Assert.Equal(1, await read.ChangeApprovals.CountAsync(a => a.ChangeId == _change.Id && a.Status == ChangeApprovalStatus.Pending, Ct));
    }

    [Fact]
    public async Task A_decided_approval_does_not_block_the_next_request()
    {
        await using var db = database.CreateDbContext();
        var first = ChangeApproval.Request(_change.Id, Fingerprint, Now);
        first.Withdraw(Now);
        db.ChangeApprovals.Add(first);
        db.ChangeApprovals.Add(ChangeApproval.Request(_change.Id, Fingerprint, Now));
        await db.SaveChangesAsync(Ct);

        Assert.Equal(2, await db.ChangeApprovals.CountAsync(a => a.ChangeId == _change.Id, Ct));
    }

    [Fact]
    public async Task Asset_link_is_unique_per_change()
    {
        await using var db = database.CreateDbContext();
        var definition = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'change_assets' AND indexdef LIKE '%UNIQUE%(change_id, asset_id)%'")
            .SingleAsync(Ct);

        Assert.Contains("(change_id, asset_id)", definition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Change_task_round_trips_with_its_phase()
    {
        var task = ChangeTask("TSK-2026-000001", ChangePhase.Backout);
        await using (var db = database.CreateDbContext())
        {
            db.WorkTasks.Add(task);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var loaded = await read.WorkTasks.SingleAsync(t => t.Id == task.Id, Ct);
        Assert.Equal((_change.Id, ChangePhase.Backout), (loaded.ChangeId, loaded.Phase));
    }

    [Theory]
    [InlineData("UPDATE work_tasks SET ticket_id = gen_random_uuid()")]
    [InlineData("UPDATE work_tasks SET phase = NULL")]
    [InlineData("UPDATE work_tasks SET phase = 'unknown'")]
    public async Task Check_constraints_reject_inconsistent_change_tasks(string sql)
    {
        await using (var db = database.CreateDbContext())
        {
            db.WorkTasks.Add(ChangeTask("TSK-2026-000001", ChangePhase.Preparation));
            await db.SaveChangesAsync(Ct);
        }

        await using var update = database.CreateDbContext();
        var error = await Assert.ThrowsAnyAsync<PostgresException>(() => update.Database.ExecuteSqlRawAsync(sql, Ct));
        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task A_phase_without_a_change_is_refused()
    {
        await using (var db = database.CreateDbContext())
        {
            db.WorkTasks.Add(WorkTask.Create("TSK-2026-000001", "Yedek al", null, _team.Id, null, Priority.P2, null, null, null, null, _author.Id, Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var update = database.CreateDbContext();
        var error = await Assert.ThrowsAnyAsync<PostgresException>(() => update.Database.ExecuteSqlRawAsync("UPDATE work_tasks SET phase = 'backout'", Ct));
        Assert.Equal(CheckViolation, error.SqlState);
    }

    private WorkTask ChangeTask(string number, ChangePhase phase) =>
        WorkTask.Create(number, "Yedek al", null, _team.Id, null, Priority.P2, null, null, _change.Id, phase, _author.Id, Now);
}
