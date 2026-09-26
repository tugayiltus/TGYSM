using Tgysm.Domain.Common;

namespace Tgysm.Domain.Organization;

public sealed class Team : Entity
{
    public const int NameMaxLength = 150;

    private Team()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    /// <summary>New tickets go to this team; at most one team has it (unique partial index).</summary>
    public bool IsDefaultIntake { get; private set; }

    public static Team Create(string name)
    {
        var team = new Team { IsActive = true };
        team.Rename(name);
        return team;
    }

    public void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = TurkishText.ToSearchKey(name);
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public void MarkDefaultIntake() => IsDefaultIntake = true;

    public void ClearDefaultIntake() => IsDefaultIntake = false;
}
