using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class WorkTaskPersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";
    private const int ParallelCreators = 8;
    private const int Year = 2026;
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private User _agent = null!;
    private Team _team = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        _agent = User.Create("Ali", "Demir", "ali@example.com", UserRole.ItStaff, null, null, "hash", Now);
        _team = Team.Create("Veritabanı");
        await using var db = database.CreateDbContext();
        db.Users.Add(_agent);
        db.Teams.Add(_team);
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private WorkTask NewTask(string number = "TSK-2026-000001", Guid? ticketId = null) =>
        WorkTask.Create(number, "Yedek al", null, _team.Id, _agent.Id, Priority.P2, new DateOnly(2026, 11, 20), ticketId, _agent.Id, Now);

    [Fact]
    public async Task Task_round_trips_with_history()
    {
        var other = Team.Create("Ağ");
        var task = NewTask();
        await using (var db = database.CreateDbContext())
        {
            db.Teams.Add(other);
            db.WorkTasks.Add(task);
            db.TaskAssignments.Add(task.Transfer(other.Id, TransferReason.Workload, "DB değil", Now, _agent.Id));
            db.TaskEvents.Add(TaskEvent.Record(task.Id, TaskEventKind.Created, _agent.Id, "{}", Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var loaded = await read.WorkTasks.SingleAsync(t => t.Id == task.Id, Ct);
        Assert.Equal(("TSK-2026-000001", WorkTaskStatus.Open, other.Id, (Guid?)null), (loaded.Number, loaded.Status, loaded.TeamId, loaded.AssigneeId));
        Assert.Equal((Priority.P2, (DateOnly?)new DateOnly(2026, 11, 20)), (loaded.Priority, loaded.DueDate));
        Assert.Equal(1, await read.TaskEvents.CountAsync(e => e.TaskId == task.Id, Ct));
        var history = await read.TaskAssignments.SingleAsync(a => a.TaskId == task.Id, Ct);
        Assert.Equal((TransferReason.Workload, "DB değil"), (history.Reason, history.Note));
        Assert.NotEqual(0u, loaded.Version);
    }

    [Fact]
    public async Task Number_is_unique()
    {
        await using (var db = database.CreateDbContext())
        {
            db.WorkTasks.Add(NewTask());
            await db.SaveChangesAsync(Ct);
        }

        await using var again = database.CreateDbContext();
        again.WorkTasks.Add(NewTask());
        // The context turns a unique violation into a domain conflict (409).
        var error = await Assert.ThrowsAsync<DomainException>(() => again.SaveChangesAsync(Ct));
        var cause = Assert.IsType<DbUpdateException>(error.InnerException);
        Assert.Equal(UniqueViolation, Assert.IsType<PostgresException>(cause.InnerException).SqlState);
    }

    [Theory]
    [InlineData("UPDATE work_tasks SET status = 'completed'")]
    [InlineData("UPDATE work_tasks SET status = 'cancelled'")]
    [InlineData("UPDATE work_tasks SET status = 'on_hold'")]
    [InlineData("UPDATE work_tasks SET priority = 'p9'")]
    public async Task Check_constraints_reject_inconsistent_rows(string sql)
    {
        await using (var db = database.CreateDbContext())
        {
            db.WorkTasks.Add(NewTask());
            await db.SaveChangesAsync(Ct);
        }

        await using var update = database.CreateDbContext();
        var error = await Assert.ThrowsAnyAsync<PostgresException>(() => update.Database.ExecuteSqlRawAsync(sql, Ct));
        Assert.Equal(CheckViolation, error.SqlState);
    }

    [Fact]
    public async Task Linked_ticket_must_exist()
    {
        await using var db = database.CreateDbContext();
        db.WorkTasks.Add(NewTask(ticketId: Guid.NewGuid()));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        Assert.Equal(ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Parallel_task_numbers_are_distinct_and_sequential()
    {
        var values = await Task.WhenAll(Enumerable.Range(0, ParallelCreators).Select(async _ =>
        {
            await using var db = database.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            var value = await new TicketNumberGenerator(db).NextAsync(TaskNumber.CounterKind, Year, Ct);
            await transaction.CommitAsync(Ct);
            return value;
        }));

        Assert.Equal(Enumerable.Range(1, ParallelCreators).Select(v => (long)v), values.Order());
    }

    [Fact]
    public async Task Task_counter_does_not_touch_ticket_counters()
    {
        await using var db = database.CreateDbContext();
        var generator = new TicketNumberGenerator(db);

        Assert.Equal(1, await generator.NextAsync(TicketKind.Incident, Year, Ct));
        Assert.Equal(1, await generator.NextAsync(TaskNumber.CounterKind, Year, Ct));
        Assert.Equal(2, await generator.NextAsync(TicketKind.Incident, Year, Ct));
    }

    [Fact]
    public async Task Outbox_row_keeps_its_subject()
    {
        var taskId = Guid.NewGuid();
        await using (var db = database.CreateDbContext())
        {
            db.OutboxEvents.Add(OutboxEvent.CreateForSubject(NotificationEventTypes.TaskAssigned, OutboxSubjectTypes.Task, taskId, _agent.Id, "{}", Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var row = await read.OutboxEvents.SingleAsync(Ct);
        Assert.Equal((OutboxSubjectTypes.Task, (Guid?)taskId, (Guid?)null), (row.SubjectType, row.SubjectId, row.TicketId));
    }
}
