using System.Reflection;
using Tgysm.Application.Reports;
using Tgysm.Infrastructure.Reports;

namespace Tgysm.Application.Tests.Reports;

/// <summary>ExportHeaders is the only server-side Turkish text (spec §7.3): every column, filter, label and enum value an export can show has a text.</summary>
public sealed class ExportHeadersCoverageTests
{
    private static readonly IReadOnlySet<string> Keys = ExportHeaders.Keys;

    [Fact]
    public void Every_column_key_has_a_header_and_covers_its_row_record()
    {
        foreach (var key in ExportColumns.Tickets.Concat(ExportColumns.Assets))
        {
            Assert.False(string.IsNullOrWhiteSpace(ExportHeaders.Column(key)), key);
        }

        Assert.Equal(typeof(DrilldownTicketRow).GetProperties().Length, ExportColumns.Tickets.Count);
        Assert.Equal(typeof(DrilldownAssetRow).GetProperties().Length - 1, ExportColumns.Assets.Count); // the asset id is not a column
    }

    [Fact]
    public void Every_enum_value_in_rows_and_filters_has_a_text()
    {
        foreach (var type in ExportHeaders.EnumTypes)
        {
            foreach (var name in Enum.GetNames(type))
            {
                var key = $"values.{ExportHeaders.GroupOf(type)}.{System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(name)}";
                Assert.Contains(key, Keys);
            }
        }
    }

    [Fact]
    public void Every_enum_a_row_record_carries_is_translatable()
    {
        var rowEnums = new[] { typeof(DrilldownTicketRow), typeof(DrilldownAssetRow) }
            .SelectMany(t => t.GetProperties())
            .Select(p => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)
            .Where(t => t.IsEnum)
            .Distinct();

        Assert.All(rowEnums, type => Assert.Contains(type, ExportHeaders.EnumTypes));
    }

    [Fact]
    public void Every_filter_key_label_and_scope_has_a_text_and_no_text_is_stale()
    {
        var filterKeys = typeof(ReportFilterKeys).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name != nameof(ReportFilterKeys.DateFormat))
            .Select(f => (string)f.GetRawConstantValue()!);
        foreach (var key in filterKeys)
        {
            Assert.Contains($"filters.{key}", Keys);
        }

        foreach (var label in typeof(ExportHeaders.Labels).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetRawConstantValue()!))
        {
            Assert.Contains($"labels.{label}", Keys);
        }

        Assert.Contains($"scope.{GetReportHandler.AllScope}", Keys);
        Assert.Contains($"scope.{GetReportHandler.TeamsScope}", Keys);

        var expected = filterKeys.Select(k => $"filters.{k}")
            .Concat(ExportColumns.Tickets.Concat(ExportColumns.Assets).Select(c => $"columns.{c}"))
            .Concat(typeof(ExportHeaders.Labels).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => $"labels.{f.GetRawConstantValue()}"))
            .Concat([$"scope.{GetReportHandler.AllScope}", $"scope.{GetReportHandler.TeamsScope}"])
            .Concat(ExportHeaders.EnumTypes.SelectMany(t => Enum.GetNames(t).Select(n => $"values.{ExportHeaders.GroupOf(t)}.{System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(n)}")))
            .ToHashSet();
        Assert.Empty(Keys.Except(expected));
    }
}
