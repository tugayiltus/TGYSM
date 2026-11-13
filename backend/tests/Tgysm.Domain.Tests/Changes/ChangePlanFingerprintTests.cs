using Tgysm.Domain.Changes;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests.Changes;

public sealed class ChangePlanFingerprintTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    private static ChangeDraft Draft(
        string impl = "Uygula", string verify = "Doğrula", string backout = "Geri al", Level risk = Level.Medium, TimeSpan? offset = null) =>
        new("Başlık", null, null, risk, Level.Low, Now.AddDays(1), Now.AddDays(1).AddHours(2), impl, verify, backout)
        {
            WindowStart = Now.AddDays(1).ToOffset(offset ?? TimeSpan.Zero),
        };

    private static ChangeRequest Change(ChangeDraft draft) =>
        ChangeRequest.Create("CHG-2026-000001", draft, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

    private static string Hash(ChangeDraft draft, Guid[]? assets = null, Guid[]? tasks = null) =>
        ChangePlanFingerprint.Compute(Change(draft), assets ?? [A, B], tasks ?? [A]);

    [Fact]
    public void Hash_is_64_lower_case_hex()
    {
        var hash = Hash(Draft());

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash.ToLowerInvariant(), hash);
    }

    [Fact]
    public void Asset_and_task_order_do_not_matter() =>
        Assert.Equal(Hash(Draft(), [A, B], [A, B]), Hash(Draft(), [B, A], [B, A]));

    [Fact]
    public void Same_instant_in_another_offset_hashes_the_same() =>
        Assert.Equal(Hash(Draft()), Hash(Draft(offset: TimeSpan.FromHours(3))));

    [Fact]
    public void Each_plan_field_changes_the_hash()
    {
        var baseline = Hash(Draft());

        Assert.NotEqual(baseline, Hash(Draft(impl: "Başka")));
        Assert.NotEqual(baseline, Hash(Draft(verify: "Başka")));
        Assert.NotEqual(baseline, Hash(Draft(backout: "Başka")));
        Assert.NotEqual(baseline, Hash(Draft(risk: Level.High)));
        Assert.NotEqual(baseline, Hash(Draft(), [A]));
        Assert.NotEqual(baseline, Hash(Draft(), tasks: [A, B]));
    }

    [Fact]
    public void Field_boundaries_are_unambiguous() =>
        Assert.NotEqual(Hash(Draft(impl: "ab", verify: "c")), Hash(Draft(impl: "a", verify: "bc")));
}
