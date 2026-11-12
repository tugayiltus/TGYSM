using Tgysm.Domain.Tasks;

namespace Tgysm.Domain.Tests.Tasks;

public sealed class TaskNumberTests
{
    [Fact]
    public void Format_pads_to_six_digits() => Assert.Equal("TSK-2026-000007", TaskNumber.Format(2026, 7));

    [Theory]
    [InlineData("TSK-2026-000001", true)]
    [InlineData("tsk-2026-1234567", true)]
    [InlineData("INC-2026-000001", false)]
    [InlineData("TSK-26-000001", false)]
    public void IsValid_accepts_only_task_numbers(string number, bool expected) => Assert.Equal(expected, TaskNumber.IsValid(number));

    [Fact]
    public void Normalize_trims_and_upper_cases() => Assert.Equal("TSK-2026-000001", TaskNumber.Normalize(" tsk-2026-000001 "));
}
