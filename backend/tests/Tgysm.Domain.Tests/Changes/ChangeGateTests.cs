using Tgysm.Domain.Changes;

namespace Tgysm.Domain.Tests.Changes;

public sealed class ChangeGateTests
{
    [Theory]
    [InlineData(ChangeStatus.Draft, false)]
    [InlineData(ChangeStatus.PendingApproval, false)]
    [InlineData(ChangeStatus.Scheduled, true)]
    [InlineData(ChangeStatus.Implementing, true)]
    [InlineData(ChangeStatus.Verifying, true)]
    [InlineData(ChangeStatus.Closed, false)]
    [InlineData(ChangeStatus.Cancelled, false)]
    public void Preparation_runs_from_scheduled_to_verifying(ChangeStatus status, bool expected) =>
        Assert.Equal(expected, ChangeGate.CanStart(status, ChangePhase.Preparation, backoutStarted: false));

    [Theory]
    [InlineData(ChangeStatus.Scheduled, false)]
    [InlineData(ChangeStatus.Implementing, true)]
    [InlineData(ChangeStatus.Verifying, false)]
    public void Implementation_runs_only_while_implementing(ChangeStatus status, bool expected) =>
        Assert.Equal(expected, ChangeGate.CanStart(status, ChangePhase.Implementation, backoutStarted: false));

    [Theory]
    [InlineData(ChangeStatus.Implementing, false)]
    [InlineData(ChangeStatus.Verifying, true)]
    [InlineData(ChangeStatus.Closed, false)]
    public void Verification_runs_only_while_verifying(ChangeStatus status, bool expected) =>
        Assert.Equal(expected, ChangeGate.CanStart(status, ChangePhase.Verification, backoutStarted: false));

    [Theory]
    [InlineData(ChangeStatus.Implementing, false, false)]
    [InlineData(ChangeStatus.Implementing, true, true)]
    [InlineData(ChangeStatus.Verifying, true, true)]
    [InlineData(ChangeStatus.Closed, true, false)]
    public void Backout_runs_only_after_the_backout_started_on_an_open_change(ChangeStatus status, bool backoutStarted, bool expected) =>
        Assert.Equal(expected, ChangeGate.CanStart(status, ChangePhase.Backout, backoutStarted));

    [Theory]
    [InlineData(ChangePhase.Implementation, ChangeStatus.Scheduled, false, false)]
    [InlineData(ChangePhase.Implementation, ChangeStatus.Implementing, false, true)]
    [InlineData(ChangePhase.Implementation, ChangeStatus.Verifying, false, true)]
    [InlineData(ChangePhase.Implementation, ChangeStatus.Closed, false, false)]
    [InlineData(ChangePhase.Verification, ChangeStatus.Implementing, false, false)]
    [InlineData(ChangePhase.Verification, ChangeStatus.Verifying, false, true)]
    [InlineData(ChangePhase.Preparation, ChangeStatus.Draft, false, false)]
    [InlineData(ChangePhase.Preparation, ChangeStatus.Scheduled, false, true)]
    [InlineData(ChangePhase.Preparation, ChangeStatus.Verifying, false, true)]
    [InlineData(ChangePhase.Preparation, ChangeStatus.Cancelled, false, false)]
    [InlineData(ChangePhase.Backout, ChangeStatus.Implementing, false, false)]
    [InlineData(ChangePhase.Backout, ChangeStatus.Verifying, true, true)]
    [InlineData(ChangePhase.Backout, ChangeStatus.Closed, true, false)]
    public void Complete_follows_the_phase_with_late_uygulama_completion(ChangePhase phase, ChangeStatus status, bool backoutStarted, bool expected) =>
        Assert.Equal(expected, ChangeGate.CanComplete(status, phase, backoutStarted));
}
