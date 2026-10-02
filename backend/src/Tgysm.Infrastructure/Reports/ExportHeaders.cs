using System.Text.Json;
using Tgysm.Application.Reports;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Tickets;

namespace Tgysm.Infrastructure.Reports;

/// <summary>
/// Turkish text of the Excel export (spec §7.3): the only server-side Turkish text in the product. It lives in the embedded
/// <c>ExportHeaders.tr.json</c>; a missing key throws, and <c>ExportHeadersCoverageTests</c> keep that from happening in production.
/// </summary>
public static class ExportHeaders
{
    public const string ResourceName = "Tgysm.Infrastructure.Reports.ExportHeaders.tr.json";

    private const string LabelsGroup = "labels";
    private const string ScopeGroup = "scope";
    private const string ColumnsGroup = "columns";
    private const string FiltersGroup = "filters";
    private const string ValuesGroup = "values";
    private const char KeySeparator = '.';

    /// <summary>Labels of the "Filtreler" sheet and the sheet names.</summary>
    public static class Labels
    {
        public const string SheetRecords = "sheet_records";
        public const string SheetFilters = "sheet_filters";
        public const string Field = "field";
        public const string Value = "value";
        public const string Report = "report";
        public const string Metric = "metric";
        public const string Bucket = "bucket";
        public const string Part = "part";
        public const string Scope = "scope";
        public const string GeneratedAt = "generated_at";
        public const string RowCount = "row_count";
    }

    /// <summary>Every enum type whose values appear in export cells or on the "Filtreler" sheet.</summary>
    public static readonly IReadOnlyList<Type> EnumTypes =
    [
        typeof(TicketKind), typeof(TicketStatus), typeof(Priority), typeof(AssetType), typeof(AssetStatus),
        typeof(SlaStateFilter), typeof(ReportName), typeof(ReportDrillPart),
    ];

    /// <summary>Which enum type a filter key's value belongs to, for the keys whose value is an enum name.</summary>
    private static readonly IReadOnlyDictionary<string, Type> FilterEnums = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        [ReportFilterKeys.Kind] = typeof(TicketKind),
        [ReportFilterKeys.Priority] = typeof(Priority),
        [ReportFilterKeys.Status] = typeof(TicketStatus),
        [ReportFilterKeys.SlaState] = typeof(SlaStateFilter),
        [ReportFilterKeys.AssetType] = typeof(AssetType),
        [ReportFilterKeys.AssetStatus] = typeof(AssetStatus),
    };

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Texts = new(Load);

    public static string Label(string key) => Get(LabelsGroup, key);

    public static string Scope(string scopeLabel) => Get(ScopeGroup, scopeLabel);

    public static string Column(string columnKey) => Get(ColumnsGroup, columnKey);

    public static string FilterName(string filterKey) => Get(FiltersGroup, filterKey);

    /// <summary>The text of an enum value ("on_hold" of <c>TicketStatus</c>), looked up by its type and snake_case name.</summary>
    public static string Value<TEnum>(TEnum value)
        where TEnum : struct, Enum => ValueOf(typeof(TEnum), ReportFilter.EnumName(value));

    /// <summary>Text of a filter value: enum-valued filters are translated, ids and days stay as they are.</summary>
    public static string FilterValue(string filterKey, string rawValue) =>
        FilterEnums.TryGetValue(filterKey, out var type) ? ValueOf(type, rawValue) : rawValue;

    /// <summary>Text of a cell value that is an enum; other cell values are returned as null for the writer to format.</summary>
    public static string? EnumCell(object value) =>
        value is Enum e && EnumTypes.Contains(e.GetType()) ? ValueOf(e.GetType(), JsonNamingPolicy.SnakeCaseLower.ConvertName(e.ToString())) : null;

    /// <summary>Snake_case group name of an enum type ("TicketStatus" is "ticket_status").</summary>
    public static string GroupOf(Type enumType) => JsonNamingPolicy.SnakeCaseLower.ConvertName(enumType.Name);

    /// <summary>All keys that have text, as <c>group.key</c> (for the coverage test).</summary>
    public static IReadOnlySet<string> Keys => Texts.Value.Keys.ToHashSet(StringComparer.Ordinal);

    private static string ValueOf(Type enumType, string name) => Get(ValuesGroup + KeySeparator + GroupOf(enumType), name);

    private static string Get(string group, string key) =>
        Texts.Value.TryGetValue(group + KeySeparator + key, out var text)
            ? text
            : throw new InvalidOperationException($"ExportHeaders.tr.json has no text for '{group}{KeySeparator}{key}'.");

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(ExportHeaders).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var document = JsonDocument.Parse(stream);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        Flatten(document.RootElement, string.Empty, texts);
        return texts;
    }

    private static void Flatten(JsonElement element, string prefix, Dictionary<string, string> texts)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix.Length == 0 ? property.Name : prefix + KeySeparator + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Flatten(property.Value, key, texts);
            }
            else
            {
                texts[key] = property.Value.GetString()!;
            }
        }
    }
}
