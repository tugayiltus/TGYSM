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

    /// <summary>
    /// Distinct variable names present in <paramref name="text"/> that are not allowed for <paramref name="eventType"/>
    /// (either not in <see cref="TemplateVariables.AllVariables"/> at all, or known but not in
    /// <see cref="TemplateVariables.AllowedFor"/> for this event). Empty when the text is valid to save.
    /// </summary>
    public static IReadOnlyList<string> Validate(string text, string eventType)
    {
        var allowed = TemplateVariables.AllowedFor(eventType);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var badNames = new List<string>();
        foreach (Match match in Placeholder().Matches(text))
        {
            var name = match.Groups[1].Value;
            if (!allowed.Contains(name) && seen.Add(name))
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
