using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class TicketPersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string CheckViolation = "23514";
    private const int ParallelCreators = 8;
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
    private User _requester = null!;
    private Team _team = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        _requester = User.Create("Ayşe", "Kaya", "ayse@example.com", UserRole.Employee, null, null, "hash", Now);
        _team = Team.Create("Servis Masası");
        await using var db = database.CreateDbContext();
        db.Users.Add(_requester);
        db.Teams.Add(_team);
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<Ticket> SavedIncidentAsync(string number = "INC-2026-000001")
    {
        var ticket = Ticket.CreateIncident(number, "Yazıcı", "Çalışmıyor", null, null, _requester.Id, _team.Id, Now);
        await using var db = database.CreateDbContext();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(Ct);
        return ticket;
    }

    [Theory]
    [InlineData("UPDATE tickets SET status = 'on_hold', hold_reason = NULL")]
    [InlineData("UPDATE tickets SET hold_reason = 'vendor'")]
    [InlineData("UPDATE tickets SET status = 'completed'")]
    [InlineData("UPDATE tickets SET status = 'cancelled'")]
    [InlineData("UPDATE tickets SET request_type_id = gen_random_uuid()")]
    [InlineData("UPDATE tickets SET priority = 'p9'")]
    public async Task Check_constraints_reject_inconsistent_rows(string sql)
    {
        await SavedIncidentAsync();
        await using var db = database.CreateDbContext();

        var error = await Assert.ThrowsAnyAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));

        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Ticket_number_is_unique()
    {
        await SavedIncidentAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => SavedIncidentAsync());

        Assert.Equal(ErrorCodes.Duplicate, error.Code);
    }

    [Fact]
    public async Task A_ticket_has_at_most_one_pending_approval()
    {
        var ticket = await SavedIncidentAsync();
        await using (var db = database.CreateDbContext())
        {
            db.Approvals.Add(Approval.Request(ticket.Id, null, Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var other = database.CreateDbContext();
        other.Approvals.Add(Approval.Request(ticket.Id, null, Now));

        Assert.Equal(ErrorCodes.Duplicate, (await Assert.ThrowsAsync<DomainException>(() => other.SaveChangesAsync(Ct))).Code);
    }

    [Fact]
    public async Task Only_one_team_is_the_default_intake()
    {
        await using (var db = database.CreateDbContext())
        {
            db.Teams.Attach(_team).Entity.MarkDefaultIntake();
            await db.SaveChangesAsync(Ct);
        }

        var second = Team.Create("Saha Destek");
        second.MarkDefaultIntake();
        await using var other = database.CreateDbContext();
        other.Teams.Add(second);

        Assert.Equal(ErrorCodes.Duplicate, (await Assert.ThrowsAsync<DomainException>(() => other.SaveChangesAsync(Ct))).Code);
    }

    [Fact]
    public async Task Parallel_number_generation_yields_distinct_sequential_values()
    {
        async Task<long> NextAsync()
        {
            await using var db = database.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            var value = await new TicketNumberGenerator(db).NextAsync(TicketKind.Incident, Now.Year, Ct);
            await transaction.CommitAsync(Ct);
            return value;
        }

        var values = await Task.WhenAll(Enumerable.Range(0, ParallelCreators).Select(_ => Task.Run(NextAsync, Ct)));
        await using var check = database.CreateDbContext();
        var request = await new TicketNumberGenerator(check).NextAsync(TicketKind.Request, Now.Year, Ct);

        Assert.Equal(Enumerable.Range(1, ParallelCreators).Select(i => (long)i), values.Order());
        Assert.Equal(1, request);
    }

    [Theory]
    [InlineData("tickets", "updated_at DESC, id DESC")]
    [InlineData("tickets", "assignee_id, updated_at DESC")]
    [InlineData("tickets", "team_id, updated_at DESC")]
    [InlineData("tickets", "gin (search_text gin_trgm_ops)")]
    [InlineData("assets", "gin (normalized_asset_tag gin_trgm_ops)")]
    [InlineData("assets", "gin (lower((model)::text) gin_trgm_ops)")]
    public async Task List_and_search_queries_have_supporting_indexes(string table, string definitionPart)
    {
        await using var db = database.CreateDbContext();

        var definitions = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = {table}")
            .ToListAsync(Ct);

        Assert.Contains(definitions, d => d.Contains(definitionPart, StringComparison.Ordinal));
    }
}
