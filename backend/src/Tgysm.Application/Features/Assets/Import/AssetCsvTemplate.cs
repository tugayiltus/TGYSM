using System.Text;

namespace Tgysm.Application.Features.Assets.Import;

/// <summary>
/// Columns of the asset import file. The headers are part of the file format people fill in, so they are Turkish
/// (spec §3.2); matching ignores case and Turkish letter case. Field names match the API's JSON names.
/// </summary>
internal static class AssetCsvTemplate
{
    public const string AssetTag = "assetTag";
    public const string Type = "type";
    public const string Brand = "brand";
    public const string Model = "model";
    public const string SerialNumber = "serialNumber";
    public const string Location = "location";
    public const string Department = "department";
    public const string Status = "status";
    public const string WarrantyEndsOn = "warrantyEndsOn";
    public const string PurchasedOn = "purchasedOn";
    public const string Notes = "notes";

    public const string LineBreak = "\r\n";
    public const char Delimiter = ',';

    /// <summary>Column order of the template: (field, Turkish header, required).</summary>
    public static readonly IReadOnlyList<(string Field, string Header, bool Required)> Columns =
    [
        (AssetTag, "Demirbaş No", true),
        (Type, "Tür", true),
        (Brand, "Marka", true),
        (Model, "Model", true),
        (SerialNumber, "Seri No", false),
        (Location, "Konum", true),
        (Department, "Departman", false),
        (Status, "Durum", false),
        (WarrantyEndsOn, "Garanti Bitiş", false),
        (PurchasedOn, "Satın Alma", false),
        (Notes, "Notlar", false),
    ];

    private const string ExampleTag = "DT-0001";
    private const string ExampleType = "laptop";
    private const string ExampleBrand = "Lenovo";
    private const string ExampleModel = "ThinkPad T14";
    private const string ExampleSerial = "SN-0001";
    private const string ExampleStatus = "in_stock";
    private const string ExampleWarranty = "2028-12-31";
    private const string ExamplePurchased = "2025-01-15";

    /// <summary>UTF-8 with BOM (so Excel opens Turkish letters correctly), header plus one example row.</summary>
    public static byte[] Build(string exampleLocationPath)
    {
        var header = string.Join(Delimiter, Columns.Select(c => c.Header));
        string[] example = [ExampleTag, ExampleType, ExampleBrand, ExampleModel, ExampleSerial, Escape(exampleLocationPath), string.Empty, ExampleStatus, ExampleWarranty, ExamplePurchased, string.Empty];
        var text = header + LineBreak + string.Join(Delimiter, example) + LineBreak;
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];
    }

    private static string Escape(string value) =>
        value.IndexOfAny([Delimiter, '"', '\r', '\n']) < 0 ? value : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
