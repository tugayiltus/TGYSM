using Tgysm.Domain.Common;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests;

public sealed class SlaPolicyTests
{
    private const int DefaultTargetMinutes = 60;
    private const int OverrideTargetMinutes = 15;
    private const int OtherTargetMinutes = 999;
    private const int DefaultWarningPercent = 75;

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid VersionId = Guid.NewGuid();
    private static readonly Guid RequestTypeId = Guid.NewGuid();

    private static SlaTarget Target(TicketKind kind, Priority priority, SlaMetric metric, int minutes = DefaultTargetMinutes, Guid? requestTypeId = null) =>
        SlaTarget.Create(VersionId, kind, priority, requestTypeId, metric, minutes, DefaultWarningPercent);

    private static List<SlaTarget> FullMatrix() =>
        (from kind in Enum.GetValues<TicketKind>()
         from priority in Enum.GetValues<Priority>()
         from metric in Enum.GetValues<SlaMetric>()
         select Target(kind, priority, metric)).ToList();

    private static SlaPolicySnapshot Snapshot(IEnumerable<SlaTarget> targets) => new(VersionId, targets.ToList(), []);

    [Fact]
    public void TargetFor_returns_the_priority_target_when_there_is_no_override()
    {
        var snapshot = Snapshot(FullMatrix());

        var target = snapshot.TargetFor(TicketKind.Incident, Priority.P2, null, SlaMetric.Resolution);

        Assert.Equal(DefaultTargetMinutes, target.TargetMinutes);
        Assert.Null(target.RequestTypeId);
    }

    [Fact]
    public void TargetFor_prefers_the_request_type_override()
    {
        var targets = FullMatrix();
        targets.Add(Target(TicketKind.Request, Priority.P3, SlaMetric.Resolution, OverrideTargetMinutes, RequestTypeId));
        var snapshot = Snapshot(targets);

        var overridden = snapshot.TargetFor(TicketKind.Request, Priority.P3, RequestTypeId, SlaMetric.Resolution);
        var otherMetric = snapshot.TargetFor(TicketKind.Request, Priority.P3, RequestTypeId, SlaMetric.FirstResponse);
        var otherType = snapshot.TargetFor(TicketKind.Request, Priority.P3, Guid.NewGuid(), SlaMetric.Resolution);

        Assert.Equal(OverrideTargetMinutes, overridden.TargetMinutes);
        Assert.Equal(DefaultTargetMinutes, otherMetric.TargetMinutes);
        Assert.Equal(DefaultTargetMinutes, otherType.TargetMinutes);
    }

    [Fact]
    public void TargetFor_ignores_an_override_of_a_different_priority()
    {
        var targets = FullMatrix();
        targets.Add(Target(TicketKind.Request, Priority.P1, SlaMetric.Resolution, OtherTargetMinutes, RequestTypeId));
        var snapshot = Snapshot(targets);

        var target = snapshot.TargetFor(TicketKind.Request, Priority.P4, RequestTypeId, SlaMetric.Resolution);

        Assert.Equal(DefaultTargetMinutes, target.TargetMinutes);
    }

    [Fact]
    public void TargetFor_throws_when_the_matrix_has_no_matching_target()
    {
        var snapshot = Snapshot([Target(TicketKind.Incident, Priority.P1, SlaMetric.Resolution)]);

        var exception = Assert.Throws<DomainException>(() => snapshot.TargetFor(TicketKind.Request, Priority.P1, null, SlaMetric.Resolution));

        Assert.Equal(ErrorCodes.SlaTargetMissing, exception.Code);
    }

    [Fact]
    public void FindMissingTargets_lists_every_gap_in_the_kind_priority_metric_matrix()
    {
        var targets = FullMatrix().Where(t => !(t.Kind == TicketKind.Request && t.Priority == Priority.P2 && t.Metric == SlaMetric.Resolution)).ToList();

        var missing = SlaPolicyVersion.FindMissingTargets(targets);

        Assert.Equal([(TicketKind.Request, Priority.P2, SlaMetric.Resolution)], missing);
    }

    [Fact]
    public void FindMissingTargets_does_not_count_a_request_type_override_as_a_base_target()
    {
        var targets = FullMatrix().Where(t => !(t.Kind == TicketKind.Request && t.Priority == Priority.P2 && t.Metric == SlaMetric.Resolution)).ToList();
        targets.Add(Target(TicketKind.Request, Priority.P2, SlaMetric.Resolution, requestTypeId: RequestTypeId));

        Assert.Single(SlaPolicyVersion.FindMissingTargets(targets));
    }

    [Fact]
    public void Create_requires_a_target_for_every_kind_priority_and_metric()
    {
        var incomplete = FullMatrix().Skip(1).ToList();

        var exception = Assert.Throws<DomainException>(() => SlaPolicyVersion.Create(1, Guid.NewGuid(), Now, incomplete, []));

        Assert.Equal(ErrorCodes.SlaTargetMissing, exception.Code);
    }

    [Fact]
    public void Create_makes_a_current_version_that_Retire_turns_off()
    {
        var version = SlaPolicyVersion.Create(2, Guid.NewGuid(), Now, FullMatrix(), []);

        Assert.True(version.IsCurrent);
        Assert.All(version.Targets, t => Assert.Equal(version.Id, t.PolicyVersionId));

        version.Retire();

        Assert.False(version.IsCurrent);
    }

    [Theory]
    [InlineData(0, DefaultWarningPercent)]
    [InlineData(DefaultTargetMinutes, 0)]
    [InlineData(DefaultTargetMinutes, 100)]
    [InlineData(SlaTarget.MaxTargetMinutes + 1, DefaultWarningPercent)]
    public void Target_rejects_a_minute_count_outside_range_and_a_warning_outside_1_to_99(int minutes, int warningPercent)
    {
        var exception = Assert.Throws<DomainException>(() =>
            SlaTarget.Create(VersionId, TicketKind.Incident, Priority.P1, null, SlaMetric.Resolution, minutes, warningPercent));

        Assert.Equal(ErrorCodes.InvalidValue, exception.Code);
    }
}
