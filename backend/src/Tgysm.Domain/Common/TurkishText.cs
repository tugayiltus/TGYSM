using System.Globalization;

namespace Tgysm.Domain.Common;

/// <summary>Turkish-aware text helpers (ADR-0006).</summary>
public static class TurkishText
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

    private const char DotlessI = 'ı';
    private const char DottedI = 'i';

    /// <summary>
    /// Trimmed, lower-cased with Turkish rules (İ→i), then dotless ı folded to i. People type "IT", "Isik" or "Işık"
    /// for the same thing, and Turkish lower-casing alone would turn ASCII "I" into "ı". Used for search and uniqueness keys.
    /// </summary>
    public static string ToSearchKey(string value) => value.Trim().ToLower(Culture).Replace(DotlessI, DottedI);
}
