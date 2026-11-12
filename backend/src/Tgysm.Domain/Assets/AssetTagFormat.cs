using System.Globalization;
using System.Text.RegularExpressions;

namespace Tgysm.Domain.Assets;

/// <summary>
/// Server-generated asset tags (ADR-0029): <c>ASSET-</c>, the company calendar year of creation, and a number counted
/// from 1 each year, zero-padded to at least four digits and never truncated. Tags are immutable and never reused; gaps
/// are normal. Every tag is generated; CSV import does not take tags.
/// </summary>
public static partial class AssetTagFormat
{
    public const string Prefix = "ASSET-";
    public const int MinimumDigits = 4;
    private static readonly string NumberFormat = new('0', MinimumDigits);

    public static string Format(int year, long number) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}{year}-{number.ToString(NumberFormat, CultureInfo.InvariantCulture)}");

    /// <summary>The company calendar year of a moment; a tag created on New Year's Eve in UTC may already count for the next year.</summary>
    public static int YearOf(DateTimeOffset instant, TimeZoneInfo companyZone) => TimeZoneInfo.ConvertTime(instant, companyZone).Year;

    /// <summary>A tag already in the generated format (compared like other tags, without case and spaces).</summary>
    public static bool IsCurrent(string tag) => CurrentPattern().IsMatch(Asset.NormalizeTag(tag));

    [GeneratedRegex(@"^ASSET-[0-9]{4}-[0-9]{4,}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrentPattern();
}
