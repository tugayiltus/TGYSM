namespace Tgysm.Domain.Changes;

/// <summary>
/// A user who submitted or changed the plan of one approval cycle (one pending row). Nobody in this set can decide that
/// row, so nobody approves a plan they changed (separation of duties).
/// </summary>
public sealed class ChangeApprovalEditor
{
    private ChangeApprovalEditor()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ApprovalId { get; private init; }
    public Guid UserId { get; private init; }

    public static ChangeApprovalEditor Create(Guid approvalId, Guid userId) => new() { ApprovalId = approvalId, UserId = userId };
}
