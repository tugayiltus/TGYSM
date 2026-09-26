using System.Reflection;
using System.Text.Json;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Tests;

/// <summary>Every API error code and audit label must have Turkish text, and errors.json must not keep stale keys (ADR-0006).</summary>
public sealed class TranslationCoverageTests
{
    private const string ErrorsFile = "errors.json";
    private const string AdminFile = "admin.json";
    private const string AuditActionsPrefix = "auditActions.";
    private const string EntityTypesPrefix = "entityTypes.";
    private const char KeySeparator = '.';

    [Fact]
    public void Every_error_code_has_a_turkish_text_and_no_translation_is_stale()
    {
        var codes = ConstantsOf(typeof(ErrorCodes));
        var keys = LeafKeys(RepositoryPaths.Locale(ErrorsFile));

        Assert.Empty(codes.Except(keys));
        Assert.Empty(keys.Except(codes));
    }

    [Fact]
    public void Every_audit_action_and_entity_type_has_a_turkish_label()
    {
        var keys = LeafKeys(RepositoryPaths.Locale(AdminFile));

        Assert.Empty(ConstantsOf(typeof(AuditActions)).Select(a => AuditActionsPrefix + a).Except(keys));
        Assert.Empty(ConstantsOf(typeof(AuditEntityTypes)).Select(t => EntityTypesPrefix + t).Except(keys));
    }

    private static HashSet<string> ConstantsOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> LeafKeys(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Collect(document.RootElement, prefix: string.Empty, keys);
        return keys;
    }

    private static void Collect(JsonElement element, string prefix, HashSet<string> keys)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix.Length == 0 ? property.Name : prefix + KeySeparator + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Collect(property.Value, key, keys);
            }
            else if (!string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                keys.Add(key);
            }
        }
    }
}
