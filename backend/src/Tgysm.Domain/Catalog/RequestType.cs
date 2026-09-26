using Tgysm.Domain.Common;

namespace Tgysm.Domain.Catalog;

/// <summary>Kind of service request; <see cref="RequiresApproval"/> starts the manager approval (spec §3.4).</summary>
public sealed class RequestType : Entity
{
    public const int NameMaxLength = 150;

    private RequestType()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool RequiresApproval { get; private set; }
    public bool IsActive { get; private set; }

    public static RequestType Create(string name, bool requiresApproval)
    {
        var type = new RequestType { IsActive = true };
        type.Update(name, requiresApproval);
        return type;
    }

    public void Update(string name, bool requiresApproval)
    {
        Name = name.Trim();
        NormalizedName = TurkishText.ToSearchKey(name);
        RequiresApproval = requiresApproval;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
