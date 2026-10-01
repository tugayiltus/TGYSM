using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Sla;

/// <summary>
/// One immutable version of the SLA policy (ADR-0018): targets and pause rules never change after creation, a change
/// creates the next version. Only <see cref="IsCurrent"/> flips, when a newer version replaces it.
/// </summary>
public sealed class SlaPolicyVersion : Entity
{
    private readonly List<SlaTarget> _targets = [];
    private readonly List<SlaPauseRule> _pauseRules = [];

    private SlaPolicyVersion()
    {
    }

    public int Number { get; private init; }

    public Guid CreatedBy { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public bool IsCurrent { get; private set; }

    public IReadOnlyList<SlaTarget> Targets => _targets;

    public IReadOnlyList<SlaPauseRule> PauseRules => _pauseRules;

    /// <summary>
    /// Builds the next current version. <paramref name="targets"/> and <paramref name="pauseRules"/> are templates:
    /// each is copied under the new version's id. The kind × priority × metric matrix must be fully covered by base
    /// targets; request-type overrides are optional.
    /// </summary>
    public static SlaPolicyVersion Create(
        int number,
        Guid createdBy,
        DateTimeOffset now,
        IEnumerable<SlaTarget> targets,
        IEnumerable<SlaPauseRule> pauseRules)
    {
        var targetList = targets.ToList();
        if (FindMissingTargets(targetList).Count > 0)
        {
            throw new DomainException(ErrorCodes.SlaTargetMissing, "Every ticket kind, priority and metric needs a target.");
        }

        var version = new SlaPolicyVersion { Number = number, CreatedBy = createdBy, CreatedAt = now, IsCurrent = true };
        version._targets.AddRange(targetList.Select(t => SlaTarget.Create(version.Id, t.Kind, t.Priority, t.RequestTypeId, t.Metric, t.TargetMinutes, t.WarningPercent)));
        version._pauseRules.AddRange(pauseRules.Select(r => SlaPauseRule.Create(version.Id, r.HoldReason, r.PausesFirstResponse, r.PausesResolution)));
        return version;
    }

    /// <summary>Base (non request-type) matrix cells that have no target.</summary>
    public static IReadOnlyList<(TicketKind Kind, Priority Priority, SlaMetric Metric)> FindMissingTargets(IEnumerable<SlaTarget> targets) =>
        FindMissingCells(targets.Where(t => t.RequestTypeId is null).Select(t => (t.Kind, t.Priority, t.Metric)));

    /// <summary>Matrix cells absent from <paramref name="coveredCells"/>; the single definition of "complete", also used by request validation.</summary>
    public static IReadOnlyList<(TicketKind Kind, Priority Priority, SlaMetric Metric)> FindMissingCells(
        IEnumerable<(TicketKind Kind, Priority Priority, SlaMetric Metric)> coveredCells)
    {
        var present = coveredCells.ToHashSet();
        return
        [
            .. from kind in Enum.GetValues<TicketKind>()
               from priority in Enum.GetValues<Priority>()
               from metric in Enum.GetValues<SlaMetric>()
               where !present.Contains((kind, priority, metric))
               select (kind, priority, metric),
        ];
    }

    /// <summary>Called when a newer version replaces this one.</summary>
    public void Retire() => IsCurrent = false;
}
