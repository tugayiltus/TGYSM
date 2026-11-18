using Tgysm.Domain.Changes;

namespace Tgysm.Domain.Tests.Changes;

public sealed class ChangeWindowTests
{
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 12, 5, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void Touching_windows_do_not_overlap()
    {
        Assert.False(ChangeWindow.Overlaps(At(8), At(10), At(10), At(12)));
        Assert.False(ChangeWindow.Overlaps(At(10), At(12), At(8), At(10)));
    }

    [Fact]
    public void Windows_sharing_a_minute_overlap()
    {
        Assert.True(ChangeWindow.Overlaps(At(8), At(10, 1), At(10), At(12)));
        Assert.True(ChangeWindow.Overlaps(At(10), At(12), At(8), At(10, 1)));
    }

    [Fact]
    public void Service_names_match_after_turkish_normalization()
    {
        Assert.True(ChangeWindow.SameService("Mail ", "MAİL"));
        Assert.True(ChangeWindow.SameService("Mail", "Maıl"));
        Assert.False(ChangeWindow.SameService("Mail", "Dosya"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", " ")]
    [InlineData("Mail", null)]
    [InlineData(" ", "Mail")]
    public void Empty_service_names_never_match(string? a, string? b) => Assert.False(ChangeWindow.SameService(a, b));

    [Fact]
    public void Conflict_statuses_are_the_states_that_hold_a_slot()
    {
        Assert.Equal([ChangeStatus.PendingApproval, ChangeStatus.Scheduled, ChangeStatus.Implementing], ChangeWindow.ConflictStatuses);
    }
}
