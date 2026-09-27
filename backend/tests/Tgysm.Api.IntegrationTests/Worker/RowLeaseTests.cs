using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Worker;

/// <summary>
/// <see cref="RowLease"/> against a throw-away table on the shared test database (not part of the production
/// model): proves the `FOR UPDATE SKIP LOCKED` claim is disjoint across concurrent claimers and that an expired
/// lease becomes claimable again (ADR-0014).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class RowLeaseTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string TableName = "lease_rows";
    private const int RowCount = 10;
    private const string PendingPredicate = "status = 'pending' AND (locked_until IS NULL OR locked_until < now())";
    private static readonly TimeSpan BlockTimeout = TimeSpan.FromSeconds(3);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class LeaseRow
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? LockedUntil { get; set; }
    }

    private sealed class LeaseTestDbContext(DbContextOptions<LeaseTestDbContext> options) : DbContext(options)
    {
        public DbSet<LeaseRow> Rows => Set<LeaseRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LeaseRow>(entity =>
            {
                entity.ToTable(TableName);
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).ValueGeneratedNever();
            });
        }
    }

    private LeaseTestDbContext CreateLeaseDbContext() =>
        new(new DbContextOptionsBuilder<LeaseTestDbContext>()
            .UseNpgsql(database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        await using var db = database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            $"CREATE TABLE IF NOT EXISTS {TableName} (id uuid PRIMARY KEY, status text NOT NULL, locked_until timestamptz NULL); "
            + $"TRUNCATE {TableName}",
            Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = database.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS {TableName}", Ct);
    }

    private async Task<List<Guid>> SeedPendingRowsAsync()
    {
        var ids = Enumerable.Range(0, RowCount).Select(_ => Guid.CreateVersion7()).ToList();
        await using var db = CreateLeaseDbContext();
        db.Rows.AddRange(ids.Select(id => new LeaseRow { Id = id, Status = "pending" }));
        await db.SaveChangesAsync(Ct);
        return ids;
    }

    [Fact]
    public async Task Two_parallel_claims_return_disjoint_sets_totalling_all_rows()
    {
        await SeedPendingRowsAsync();

        async Task<List<Guid>> ClaimAsync()
        {
            await using var db = CreateLeaseDbContext();
            var claimed = await RowLease.ClaimAsync(db.Rows, PendingPredicate, RowCount / 2, TimeSpan.FromMinutes(5), Ct);
            return claimed.Select(r => r.Id).ToList();
        }

        var results = await Task.WhenAll(ClaimAsync(), ClaimAsync());
        var first = results[0];
        var second = results[1];

        Assert.Equal(RowCount / 2, first.Count);
        Assert.Equal(RowCount / 2, second.Count);
        Assert.Empty(first.Intersect(second));
        Assert.Equal(RowCount, first.Union(second).Count());
    }

    /// <summary>
    /// A deterministic (non-racy) proof of the `SKIP LOCKED` behavior: a row another transaction is holding via
    /// `FOR UPDATE`, uncommitted, must be skipped rather than waited on. Without `SKIP LOCKED` this claim would
    /// block until the holder commits or rolls back, so it would not finish before <see cref="BlockTimeout"/>.
    /// </summary>
    [Fact]
    public async Task Rows_locked_by_another_transaction_are_skipped_not_waited_on()
    {
        var ids = await SeedPendingRowsAsync();
        var heldIds = ids.Take(RowCount / 2).ToArray();

        await using var holder = new NpgsqlConnection(database.ConnectionString);
        await holder.OpenAsync(Ct);
        await using var holderTransaction = await holder.BeginTransactionAsync(Ct);
        await using (var lockCommand = new NpgsqlCommand($"SELECT 1 FROM {TableName} WHERE id = ANY(@ids) FOR UPDATE", holder, holderTransaction))
        {
            lockCommand.Parameters.AddWithValue("ids", heldIds);
            await using var reader = await lockCommand.ExecuteReaderAsync(Ct);
            while (await reader.ReadAsync(Ct))
            {
                // Draining is enough to take the row locks; the transaction stays open (uncommitted) below.
            }
        }

        try
        {
            await using var claimDb = CreateLeaseDbContext();
            var claimTask = RowLease.ClaimAsync(claimDb.Rows, PendingPredicate, RowCount, TimeSpan.FromMinutes(5), Ct);
            var winner = await Task.WhenAny(claimTask, Task.Delay(BlockTimeout, Ct));

            Assert.Same(claimTask, winner);
            var claimed = await claimTask;
            Assert.DoesNotContain(claimed, r => heldIds.Contains(r.Id));
            Assert.Equal(RowCount - heldIds.Length, claimed.Count);
        }
        finally
        {
            await holderTransaction.RollbackAsync(Ct);
        }
    }

    [Fact]
    public async Task Expired_lease_is_claimable_again()
    {
        var ids = await SeedPendingRowsAsync();

        await using (var db = CreateLeaseDbContext())
        {
            var firstClaim = await RowLease.ClaimAsync(db.Rows, PendingPredicate, RowCount, TimeSpan.FromMinutes(5), Ct);
            Assert.Equal(RowCount, firstClaim.Count);
        }

        // Simulate the lease having expired a long time ago, rather than waiting out a real one.
        await using (var db = database.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync($"UPDATE {TableName} SET locked_until = now() - interval '1 hour'", Ct);
        }

        await using var second = CreateLeaseDbContext();
        var secondClaim = await RowLease.ClaimAsync(second.Rows, PendingPredicate, RowCount, TimeSpan.FromMinutes(5), Ct);

        Assert.Equal(ids.OrderBy(i => i), secondClaim.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public async Task Lease_under_one_second_is_rejected()
    {
        await SeedPendingRowsAsync();
        await using var db = CreateLeaseDbContext();

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => RowLease.ClaimAsync(db.Rows, PendingPredicate, RowCount, TimeSpan.FromMilliseconds(1), Ct));

        Assert.Equal("lease", error.ParamName);
    }

    [Fact]
    public async Task Claim_inside_an_already_open_transaction_reuses_it_instead_of_nesting()
    {
        var ids = await SeedPendingRowsAsync();
        await using var db = CreateLeaseDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);

        var claimed = await RowLease.ClaimAsync(db.Rows, PendingPredicate, RowCount, TimeSpan.FromMinutes(5), Ct);
        await transaction.CommitAsync(Ct);

        Assert.Equal(ids.OrderBy(i => i), claimed.Select(r => r.Id).OrderBy(i => i));
    }

    [Fact]
    public async Task Claim_does_not_take_rows_outside_the_predicate()
    {
        await using (var db = CreateLeaseDbContext())
        {
            db.Rows.Add(new LeaseRow { Id = Guid.CreateVersion7(), Status = "done" });
            await db.SaveChangesAsync(Ct);
        }

        await using var claimDb = CreateLeaseDbContext();
        var claimed = await RowLease.ClaimAsync(claimDb.Rows, PendingPredicate, RowCount, TimeSpan.FromMinutes(5), Ct);

        Assert.Empty(claimed);
    }
}
