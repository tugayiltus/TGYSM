using Microsoft.Extensions.Time.Testing;
using Tgysm.Application.Reports;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>How a seeded change ends: closed with an outcome, cancelled, or left in a state before closing.</summary>
internal enum ChangeEnding
{
    Open,
    Successful,
    RolledBack,
    Failed,
    Cancelled,
}

/// <summary>Change seeding and report running for the change report tests; changes go through the real domain transitions.</summary>
internal sealed class ChangeReportFixture(ReportWorld world)
{
    private static readonly ChangePhaseTally NoTasks = new(0, 0, 0);
    private static readonly ChangePhaseTally OneDone = new(0, 1, 0);

    private int _counter;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReportWorld World { get; } = world;

    /// <summary>Stores a change created at <paramref name="createdAt"/> that ends as <paramref name="ending"/> at <paramref name="endedAt"/>.</summary>
    public async Task<ChangeRequest> ChangeAsync(
        ChangeEnding ending, DateTimeOffset createdAt, DateTimeOffset? endedAt = null, Level risk = Level.Medium, Guid? teamId = null)
    {
        var closedAt = endedAt ?? createdAt.AddDays(1);
        var draft = new ChangeDraft(
            $"Değişiklik {++_counter}", null, null, risk, Level.Low, createdAt.AddHours(2), createdAt.AddHours(4), "uygula", "dogrula", "geri al");
        var change = ChangeRequest.Create(
            $"CHG-2026-{_counter:D6}", draft, teamId ?? World.DeskTeam.Id, World.Desk.Id, World.Desk.Id, createdAt);
        switch (ending)
        {
            case ChangeEnding.Cancelled:
                change.Cancel("vazgecildi", closedAt);
                break;
            case ChangeEnding.Open:
                break;
            default:
                Advance(change, ending, createdAt, closedAt);
                break;
        }

        await using var db = World.Db();
        db.ChangeRequests.Add(change);
        await db.SaveChangesAsync(Ct);
        return change;
    }

    private static void Advance(ChangeRequest change, ChangeEnding ending, DateTimeOffset createdAt, DateTimeOffset closedAt)
    {
        change.Submit(1, createdAt);
        change.Schedule(createdAt);
        change.StartImplementation(createdAt.AddHours(2));
        if (ending == ChangeEnding.Successful)
        {
            change.FinishImplementation(createdAt.AddHours(3));
            change.Close(ChangeOutcome.Successful, new ChangeClosing("dogrulandi", null, null, null), new ChangeTaskTally(NoTasks, OneDone, NoTasks, NoTasks), closedAt);
            return;
        }

        change.StartBackout(createdAt.AddHours(3));
        var outcome = ending == ChangeEnding.RolledBack ? ChangeOutcome.RolledBack : ChangeOutcome.Failed;
        change.Close(outcome, new ChangeClosing(null, "geri alindi", null, null), new ChangeTaskTally(NoTasks, OneDone, NoTasks, OneDone), closedAt);
    }

    public async Task<ReportResult> RunAsync(ReportName report, Guid userId, ReportFilter? filter = null, DateTimeOffset? now = null)
    {
        await using var db = World.Db();
        var handler = new GetReportHandler(
            new ReportScope(db, new FixedActor(userId)), new TicketReportSource(db, new TestClock()), db, new FakeTimeProvider(now ?? AssetReportFixture.Now), new TestClock());
        return await handler.HandleAsync(new GetReportQuery(report, filter ?? new ReportFilter()), Ct);
    }
}
