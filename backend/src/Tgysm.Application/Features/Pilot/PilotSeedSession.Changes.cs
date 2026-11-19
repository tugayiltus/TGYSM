using Tgysm.Application.Features.Changes;
using Tgysm.Application.Features.Tasks;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Pilot;

/// <summary>Where a seeded change ends up; closed outcomes are left out, as closing needs finished work.</summary>
internal enum PilotChangePlan
{
    Draft,
    PendingApproval,
    Scheduled,
}

/// <summary>Where a seeded work task ends up.</summary>
internal enum PilotTaskPlan
{
    Open,
    InProgress,
    Completed,
    Cancelled,
}

/// <summary>Changes (with windows that overlap on shared assets and services) and work tasks of the pilot seed.</summary>
internal sealed partial class PilotSeedSession
{
    // Windows lie around the real "now", so the calendar and "this week" queue have data: past, present and planned.
    private const int FirstWindowDayOffset = -20;
    private const int LastWindowDayOffset = 55;
    private const int WindowStartHourUtc = 15;
    private const int WindowStartHourSpan = 6;
    private static readonly (int Min, int Max) WindowDuration = (60, 6 * 60);

    // A few assets are touched by many changes; every third change reuses the previous window and asset to force a clash.
    private const int SharedAssetPool = 12;
    private const int ForcedOverlapEvery = 3;
    private static readonly TimeSpan ForcedOverlapShift = TimeSpan.FromMinutes(30);
    private const int SecondAssetPercent = 30;
    private const int VerificationTaskPercent = 40;

    // The change is written this long ago (minutes), the task this long ago.
    private static readonly (int Min, int Max) ChangeAge = (MinutesPerDay, 25 * MinutesPerDay);
    private static readonly (int Min, int Max) TaskAge = (MinutesPerDay, 60 * MinutesPerDay);
    private static readonly (int Min, int Max) TaskDueDayOffset = (-30, 21);
    private static readonly (int Min, int Max) TaskStepDelay = (30, 2 * MinutesPerDay);

    // One block of each cycle, mixed so every kind shows up early at any scale (mostly scheduled and open work).
    private static readonly PilotChangePlan[] ChangePlanBlock =
    [
        PilotChangePlan.Scheduled, PilotChangePlan.Scheduled, PilotChangePlan.PendingApproval, PilotChangePlan.Scheduled, PilotChangePlan.Draft,
        PilotChangePlan.Scheduled, PilotChangePlan.Scheduled, PilotChangePlan.PendingApproval, PilotChangePlan.Scheduled,
    ];

    private static readonly PilotTaskPlan[] TaskPlanBlock =
    [
        PilotTaskPlan.Open, PilotTaskPlan.InProgress, PilotTaskPlan.Completed, PilotTaskPlan.Open, PilotTaskPlan.Cancelled,
        PilotTaskPlan.Completed, PilotTaskPlan.Open, PilotTaskPlan.InProgress, PilotTaskPlan.Completed, PilotTaskPlan.Open,
    ];

    private DateTimeOffset _previousWindowStart;
    private Guid? _previousAssetId;

    private async Task SeedChangesAsync()
    {
        var count = PilotSeedVolumes.Scaled(PilotSeedVolumes.Changes, scale);
        var pool = _assetIds.Take(SharedAssetPool).ToList();
        for (var index = 0; index < count; index++)
        {
            await SeedChangeAsync(index, ChangePlanBlock[index % ChangePlanBlock.Length], pool);
        }
    }

    private async Task SeedChangeAsync(int index, PilotChangePlan plan, IReadOnlyList<Guid> assetPool)
    {
        var inIntake = index % 2 == 0;
        var teamId = inIntake ? _intakeTeamId : _fieldTeamId;
        var owner = Pick(inIntake ? _intakeMembers : _fieldMembers);
        var at = Clamp(realNow.AddMinutes(-_random.Next(ChangeAge.Min, ChangeAge.Max)));

        var forceOverlap = index > 0 && index % ForcedOverlapEvery == 0 && _previousAssetId is not null;
        var start = forceOverlap ? _previousWindowStart + ForcedOverlapShift : RandomWindowStart();
        var end = start.AddMinutes(_random.Next(WindowDuration.Min, WindowDuration.Max));
        var assets = new List<Guid> { forceOverlap ? _previousAssetId!.Value : Pick(assetPool) };
        if (_random.Next(Hundred) < SecondAssetPercent)
        {
            var second = Pick(assetPool);
            if (!assets.Contains(second))
            {
                assets.Add(second);
            }
        }

        _previousWindowStart = start;
        _previousAssetId = assets[0];

        var draft = new ChangeDraft(
            $"{Pick(PilotSeedChangeData.ChangeTitles)} ({index + 1})", PilotSeedChangeData.ChangeJustification, Pick(PilotSeedChangeData.AffectedServices),
            Pick(Enum.GetValues<Level>()), Pick(Enum.GetValues<Level>()), start, end,
            PilotSeedChangeData.ImplementationPlan, PilotSeedChangeData.VerificationPlan, PilotSeedChangeData.BackoutPlan);
        var created = await ActAsync<CreateChangeHandler, ChangeDetail>(owner, at, (h, ct) => h.HandleAsync(new CreateChangeCommand(draft, teamId, owner, assets), ct));
        if (plan == PilotChangePlan.Draft)
        {
            return;
        }

        at = Later(at, TaskStepDelay);
        await AddChangeTaskAsync(created.Number, ChangePhase.Implementation, PilotSeedChangeData.ImplementationTaskTitle, teamId, owner, at);
        if (_random.Next(Hundred) < VerificationTaskPercent)
        {
            await AddChangeTaskAsync(created.Number, ChangePhase.Verification, PilotSeedChangeData.VerificationTaskTitle, teamId, owner, at);
        }

        var ready = (await ActAsync<GetChangeHandler, ChangeView>(owner, at, (h, ct) => h.HandleAsync(new GetChangeQuery(created.Number), ct))).Detail!;
        at = Later(at, TaskStepDelay);
        var submitted = await ActAsync<SubmitChangeHandler, ChangeDetail>(owner, at, (h, ct) => h.HandleAsync(new SubmitChangeCommand(ready.Number, ready.Version), ct));
        if (plan == PilotChangePlan.PendingApproval)
        {
            return;
        }

        at = Later(at, TaskStepDelay);
        var decision = new DecideChangeApprovalCommand(submitted.Number, ChangeApprovalDecision.Approve, submitted.Approval!.Version, null);
        await ActAsync<DecideChangeApprovalHandler, ChangeDetail>(Pick(_approvers), at, (h, ct) => h.HandleAsync(decision, ct));
    }

    private Task<TaskDetail> AddChangeTaskAsync(string changeNumber, ChangePhase phase, string title, Guid teamId, Guid owner, DateTimeOffset at)
    {
        var command = new CreateTaskCommand(title, null, teamId, owner, Priority.P3, null, null, changeNumber, phase);
        return ActAsync<CreateTaskHandler, TaskDetail>(owner, at, (h, ct) => h.HandleAsync(command, ct));
    }

    private DateTimeOffset RandomWindowStart() =>
        new DateTimeOffset(realNow.UtcDateTime.Date, TimeSpan.Zero)
            .AddDays(_random.Next(FirstWindowDayOffset, LastWindowDayOffset))
            .AddHours(WindowStartHourUtc + _random.Next(WindowStartHourSpan));

    private async Task SeedTasksAsync()
    {
        var count = PilotSeedVolumes.Scaled(PilotSeedVolumes.StandaloneTasks, scale);
        for (var index = 0; index < count; index++)
        {
            await SeedTaskAsync(index, TaskPlanBlock[index % TaskPlanBlock.Length]);
        }
    }

    private async Task SeedTaskAsync(int index, PilotTaskPlan plan)
    {
        var inIntake = index % 2 == 0;
        var teamId = inIntake ? _intakeTeamId : _fieldTeamId;
        var members = inIntake ? _intakeMembers : _fieldMembers;
        var creator = Pick(members);
        var assignee = Pick(members);
        var at = Clamp(realNow.AddMinutes(-_random.Next(TaskAge.Min, TaskAge.Max)));
        var due = DateOnly.FromDateTime(realNow.UtcDateTime).AddDays(_random.Next(TaskDueDayOffset.Min, TaskDueDayOffset.Max));
        var command = new CreateTaskCommand(
            Pick(PilotSeedChangeData.TaskTitles), PilotSeedChangeData.TaskDescription, teamId, assignee, Pick(Enum.GetValues<Priority>()), due, null);
        var task = await ActAsync<CreateTaskHandler, TaskDetail>(creator, at, (h, ct) => h.HandleAsync(command, ct));
        if (plan == PilotTaskPlan.Open)
        {
            return;
        }

        at = Later(at, TaskStepDelay);
        if (plan == PilotTaskPlan.Cancelled)
        {
            await ActAsync<CancelTaskHandler, TaskDetail>(assignee, at, (h, ct) => h.HandleAsync(new CancelTaskCommand(task.Number, task.Version, PilotSeedChangeData.TaskCancelReason), ct));
            return;
        }

        var started = await ActAsync<StartTaskHandler, TaskDetail>(assignee, at, (h, ct) => h.HandleAsync(new StartTaskCommand(task.Number, task.Version), ct));
        if (plan == PilotTaskPlan.InProgress)
        {
            return;
        }

        at = Later(at, TaskStepDelay);
        await ActAsync<CompleteTaskHandler, TaskDetail>(assignee, at, (h, ct) => h.HandleAsync(new CompleteTaskCommand(started.Number, started.Version, PilotSeedChangeData.TaskResultNote), ct));
    }
}
