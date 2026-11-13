using System.Globalization;
using System.Text.RegularExpressions;

namespace Tgysm.Domain.Changes;

/// <summary>Human change numbers: <c>CHG-2026-000001</c>, from the shared counter table (ADR-0011, ADR-0032).</summary>
public static partial class ChangeNumber
{
    public const string Prefix = "CHG";

    /// <summary>Counter row kind in <c>ticket_counters</c>.</summary>
    public const string CounterKind = "change";
    private const string ValueFormat = "D6";

    public static string Format(int year, long value) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}-{year}-{value.ToString(ValueFormat, CultureInfo.InvariantCulture)}");

    public static bool IsValid(string number) => NumberPattern().IsMatch(number);

    public static string Normalize(string number) => number.Trim().ToUpperInvariant();

    [GeneratedRegex("^CHG-[0-9]{4}-[0-9]{6,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();
}
