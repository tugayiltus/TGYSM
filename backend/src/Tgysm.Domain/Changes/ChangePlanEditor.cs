namespace Tgysm.Domain.Changes;

/// <summary>
/// A user who submitted the change or changed its plan at any time in its life (ADR-0032). Nobody in this set ever
/// decides an approval of the change, so nobody approves a plan they shaped (separation of duties).
/// </summary>
public sealed class ChangePlanEditor
{
    private ChangePlanEditor()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ChangeId { get; private init; }
    public Guid UserId { get; private init; }

    public static ChangePlanEditor Create(Guid changeId, Guid userId) => new() { ChangeId = changeId, UserId = userId };
}
