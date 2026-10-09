using System.Globalization;

namespace Tgysm.Domain.Assets;

/// <summary>
/// Server-generated asset tags (ADR-0023 amendment): <c>DMR-</c> plus a sequence number, zero-padded to six digits and
/// never truncated. Tags are immutable and never reused; gaps are normal. The prefix is reserved for generated tags.
/// </summary>
public static class AssetTagFormat
{
    public const string Prefix = "DMR-";
    public const int Digits = 6;

    private static readonly string NumberFormat = new('0', Digits);

    public static string Format(long number) => Prefix + number.ToString(NumberFormat, CultureInfo.InvariantCulture);

    /// <summary>A tag that uses the generated prefix (compared like other tags, without case and spaces).</summary>
    public static bool IsReserved(string tag) => Asset.NormalizeTag(tag).StartsWith(Prefix, StringComparison.Ordinal);
}
