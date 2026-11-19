using Microsoft.Extensions.Time.Testing;
using Tgysm.Application.Reports;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>How a seeded task ends: left open, started, put on hold, completed or cancelled.</summary>
internal enum TaskEnding
{
    Open,
    InProgress,
    OnHold,
    Completed,
    Cancelled,
}

/// <summary>Task seeding and report running for the task report tests; tasks go through the real domain transitions.</summary>
internal sealed class TaskReportFixture(ReportWorld world)
{
    private int _counter;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReportWorld World { get; } = world;

    /// <summary>Stores a task created at <paramref name="createdAt"/> that reaches <paramref name="ending"/> <paramref name="after"/> later.</summary>
    public async Task<WorkTask> TaskAsync(
        TaskEnding ending, DateTimeOffset createdAt, DateOnly? dueDate = null, TimeSpan? after = null, Guid? teamId = null)
    {
        var endedAt = createdAt + (after ?? TimeSpan.FromHours(1));
        var task = WorkTask.Create(
            $"TSK-2026-{++_counter:D6}", $"Görev {_counter}", null, teamId ?? World.DeskTeam.Id, null, Priority.P2, dueDate, null, null, null, World.Desk.Id, createdAt);
        switch (ending)
        {
            case TaskEnding.InProgress:
                task.Start(endedAt);
                break;
            case TaskEnding.OnHold:
                task.Hold(HoldReason.Vendor, null, endedAt);
                break;
            case TaskEnding.Completed:
                task.Complete("bitti", endedAt);
                break;
            case TaskEnding.Cancelled:
                task.Cancel("vazgecildi", endedAt);
                break;
        }

        await using var db = World.Db();
        db.WorkTasks.Add(task);
        await db.SaveChangesAsync(Ct);
        return task;
    }

    public async Task<ReportResult> RunAsync(ReportName report, Guid userId, ReportFilter? filter = null, DateTimeOffset? now = null)
    {
        await using var db = World.Db();
        var handler = new GetReportHandler(
            new ReportScope(db, new FixedActor(userId)), new TicketReportSource(db, new TestClock()), db, new FakeTimeProvider(now ?? AssetReportFixture.Now), new TestClock());
        return await handler.HandleAsync(new GetReportQuery(report, filter ?? new ReportFilter()), Ct);
    }
}
