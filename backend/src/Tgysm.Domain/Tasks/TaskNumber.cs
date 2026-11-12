using System.Globalization;
using System.Text.RegularExpressions;

namespace Tgysm.Domain.Tasks;

/// <summary>Human task numbers: <c>TSK-2026-000001</c>, from the shared counter table (ADR-0011, ADR-0031).</summary>
public static partial class TaskNumber
{
    public const string Prefix = "TSK";

    /// <summary>Counter row kind in <c>ticket_counters</c>.</summary>
    public const string CounterKind = "task";
    private const string ValueFormat = "D6";

    public static string Format(int year, long value) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}-{year}-{value.ToString(ValueFormat, CultureInfo.InvariantCulture)}");

    public static bool IsValid(string number) => NumberPattern().IsMatch(number);

    public static string Normalize(string number) => number.Trim().ToUpperInvariant();

    [GeneratedRegex("^TSK-[0-9]{4}-[0-9]{6,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();
}
