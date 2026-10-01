namespace Tgysm.Domain.Sla;

/// <summary>A date that is otherwise a working day but counts as closed. Unique per date.</summary>
public sealed class Holiday : Common.Entity
{
    public const int NameMaxLength = 200;

    private Holiday()
    {
    }

    public DateOnly Date { get; private init; }

    public string Name { get; private set; } = string.Empty;

    public static Holiday Create(DateOnly date, string name) => new() { Date = date, Name = name.Trim() };
}
