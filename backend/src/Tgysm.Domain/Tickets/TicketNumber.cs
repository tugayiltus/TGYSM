using System.Globalization;
using System.Text.RegularExpressions;

namespace Tgysm.Domain.Tickets;

/// <summary>Human ticket numbers: <c>INC-2026-000001</c>, <c>REQ-2026-000001</c> (ADR-0011).</summary>
public static partial class TicketNumber
{
    public const int MaxLength = 32;
    public const string IncidentPrefix = "INC";
    public const string RequestPrefix = "REQ";
    private const string ValueFormat = "D6";

    public static string Format(TicketKind kind, int year, long value) =>
        string.Create(CultureInfo.InvariantCulture, $"{PrefixOf(kind)}-{year}-{value.ToString(ValueFormat, CultureInfo.InvariantCulture)}");

    public static string PrefixOf(TicketKind kind) => kind == TicketKind.Incident ? IncidentPrefix : RequestPrefix;

    public static bool IsValid(string number) => NumberPattern().IsMatch(number);

    /// <summary>Upper-cases a number typed by a person, so lookups are case-insensitive.</summary>
    public static string Normalize(string number) => number.Trim().ToUpperInvariant();

    [GeneratedRegex("^(INC|REQ)-[0-9]{4}-[0-9]{6,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();
}
