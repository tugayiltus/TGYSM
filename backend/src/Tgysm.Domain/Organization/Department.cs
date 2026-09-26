using Tgysm.Domain.Common;

namespace Tgysm.Domain.Organization;

public sealed class Department : Entity
{
    public const int NameMaxLength = 150;

    private Department()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Admin who approves requests of this department (used from Phase 3).</summary>
    public Guid? ApproverUserId { get; private set; }

    public bool IsActive { get; private set; }

    public static Department Create(string name, Guid? approverUserId)
    {
        var department = new Department { IsActive = true };
        department.Update(name, approverUserId);
        return department;
    }

    public void Update(string name, Guid? approverUserId)
    {
        Name = name.Trim();
        NormalizedName = TurkishText.ToSearchKey(name);
        ApproverUserId = approverUserId;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
