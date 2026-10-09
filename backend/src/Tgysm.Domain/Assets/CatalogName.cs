using System.Text.RegularExpressions;
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Assets;

/// <summary>
/// Uniqueness key of brand and model names: inner whitespace collapsed, then <see cref="TurkishText.ToSearchKey"/>.
/// Punctuation and digits are kept, so "ThinkPad T14" and "ThinkPad T14s" stay different models.
/// </summary>
public static partial class CatalogName
{
    public const int MaxLength = 100;

    private const string SingleSpace = " ";

    public static string Normalize(string name) => TurkishText.ToSearchKey(Whitespace().Replace(name, SingleSpace));

    /// <summary>Display form: trimmed with inner whitespace collapsed.</summary>
    public static string Clean(string name) => Whitespace().Replace(name.Trim(), SingleSpace);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
