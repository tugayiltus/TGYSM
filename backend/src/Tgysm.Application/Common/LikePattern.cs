namespace Tgysm.Application.Common;

/// <summary>
/// Builds <c>LIKE</c> patterns for <c>EF.Functions.Like</c>. Substring search must go through <c>LIKE '%…%'</c>: Npgsql
/// translates <c>string.Contains</c> to <c>strpos</c>, which cannot use the <c>gin_trgm_ops</c> search indexes.
/// </summary>
public static class LikePattern
{
    /// <summary>
    /// Escape character for the patterns built here. Pass it as <c>EF.Functions.Like(value, pattern, LikePattern.EscapeText)</c>:
    /// without it Npgsql emits <c>ESCAPE ''</c>, which turns escaping off.
    /// </summary>
    public const string EscapeText = @"\";

    private const char EscapeCharacter = '\\';

    private const char AnyText = '%';
    private const char AnyCharacter = '_';

    /// <summary>Pattern matching <paramref name="text"/> anywhere, with the text taken literally.</summary>
    public static string Containing(string text) => AnyText + Escape(text) + AnyText;

    /// <summary>Escapes the wildcards and the escape character itself so they match literally.</summary>
    private static string Escape(string text) => text
        .Replace(EscapeCharacter.ToString(), $"{EscapeCharacter}{EscapeCharacter}", StringComparison.Ordinal)
        .Replace(AnyText.ToString(), $"{EscapeCharacter}{AnyText}", StringComparison.Ordinal)
        .Replace(AnyCharacter.ToString(), $"{EscapeCharacter}{AnyCharacter}", StringComparison.Ordinal);
}
