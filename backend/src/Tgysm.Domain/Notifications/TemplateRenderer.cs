using System.Text.RegularExpressions;

namespace Tgysm.Domain.Notifications;

/// <summary>
/// Plain-text `{{name}}` substitution for email templates (spec §3.4, ADR-0016). No expressions are evaluated, and
/// a replacement value is inserted literally without being re-scanned for further placeholders (no recursion).
/// </summary>
public static partial class TemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9]*)\s*\}\}")]
    private static partial Regex Placeholder();

    // Matches every `{{...}}` region, not just ones that already look like a valid name, so a malformed
    // placeholder (wrong characters, leading digit, ...) is caught at save time instead of rendering raw.
    [GeneratedRegex(@"\{\{(.*?)\}\}")]
    private static partial Regex AnyPlaceholder();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*$")]
    private static partial Regex ValidName();

    /// <summary>
    /// Distinct placeholder contents present in <paramref name="text"/> that are not valid to save: either the
    /// text between `{{` and `}}` is not a valid variable-name shape at all, or it is a valid shape but not in
    /// <see cref="TemplateVariables.AllVariables"/>, or known but not in <see cref="TemplateVariables.AllowedFor"/>
    /// for <paramref name="eventType"/>. Empty when the text is valid to save.
    /// </summary>
    public static IReadOnlyList<string> Validate(string text, string eventType)
    {
        var allowed = TemplateVariables.AllowedFor(eventType);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var badNames = new List<string>();
        foreach (Match match in AnyPlaceholder().Matches(text))
        {
            var name = match.Groups[1].Value.Trim();
            var isBad = !ValidName().IsMatch(name) || !allowed.Contains(name);
            if (isBad && seen.Add(name))
            {
                badNames.Add(name);
            }
        }

        return badNames;
    }

    /// <summary>Replaces every `{{name}}` placeholder with its value from <paramref name="values"/>; a missing value becomes empty text.</summary>
    public static string Render(string text, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(text, match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : string.Empty);
}
