using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Tgysm.Infrastructure.Persistence.Converters;

/// <summary>Stores an enum as its snake_case name ("InStock" → "in_stock"), matching the API JSON names.</summary>
internal static class SnakeCaseEnumConversion<TEnum>
    where TEnum : struct, Enum
{
    public const int MaxLength = 40;

    private static readonly Dictionary<TEnum, string> ToName =
        Enum.GetValues<TEnum>().ToDictionary(value => value, value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));

    private static readonly Dictionary<string, TEnum> FromName = ToName.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static readonly ValueConverter<TEnum, string> Converter = new(value => ToName[value], name => FromName[name]);

    /// <summary>SQL CHECK body allowing exactly the enum's names.</summary>
    public static string CheckSql(string column) => CheckSql(column, []);

    /// <summary>SQL CHECK body allowing the enum's names except those of <paramref name="excluded"/>.</summary>
    public static string CheckSql(string column, IReadOnlyCollection<TEnum> excluded) =>
        $"{column} IN ({string.Join(", ", ToName.Where(pair => !excluded.Contains(pair.Key)).Select(pair => $"'{pair.Value}'"))})";
}
