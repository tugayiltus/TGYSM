using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Assets;

[Collection(ApiCollection.Name)]
public sealed class AssetImportTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ImportPath = "/api/v1/assets/import";
    private const string PreviewPath = ImportPath + "/preview";
    private const string TemplatePath = ImportPath + "/template";
    private const string CsvType = "text/csv";
    private const string FileName = "cihazlar.csv";
    private const string Header = "Demirbaş No,Tür,Marka,Model,Seri No,Konum,Departman,Durum,Garanti Bitiş,Satın Alma,Notlar";
    private const int MaxRows = 5_000;
    private const int MaxBytes = 10 * 1024 * 1024;
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        var campus = await TestData.LocationAsync(factory, "Merkez Kampüs");
        await TestData.LocationAsync(factory, "A Blok", campus.Id);
        var branch = await TestData.LocationAsync(factory, "Şube");
        await TestData.LocationAsync(factory, "A Blok", branch.Id);
        await TestData.LocationAsync(factory, "Işık Binası");
        await TestData.LocationAsync(factory, "Eski Depo", active: false);
        await TestData.DepartmentAsync(factory, "Muhasebe");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> ItAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com")).Client;

    private static byte[] Csv(params string[] rows) => Encoding.UTF8.GetBytes(string.Join("\r\n", [Header, .. rows]));

    private static async Task<JsonElement> PreviewAsync(ApiClient client, byte[] content)
    {
        using var response = await client.PostFileAsync(PreviewPath, FileName, content, CsvType);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static string CommitPath(JsonElement job) => $"{ImportPath}/{job.GetProperty("jobId").GetGuid()}/commit";

    private static List<(int Row, string Field, string Code)> ErrorsOf(JsonElement job) =>
        [.. job.GetProperty("errors").EnumerateArray().Select(e =>
            (e.GetProperty("row").GetInt32(), e.GetProperty("field").GetString()!, e.GetProperty("code").GetString()!))];

    [Fact]
    public async Task Employee_cannot_import()
    {
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "e@example.com");

        using var template = await employee.GetAsync(TemplatePath);
        using var preview = await employee.PostFileAsync(PreviewPath, FileName, Csv(), CsvType);

        Assert.Equal(HttpStatusCode.Forbidden, template.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
    }

    [Fact]
    public async Task Template_is_utf8_with_bom_and_turkish_headers()
    {
        var it = await ItAsync();

        using var response = await it.GetAsync(TemplatePath);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith(CsvType, response.Content.Headers.ContentType?.ToString());
        Assert.Equal(Bom, bytes[..Bom.Length]);
        var lines = Encoding.UTF8.GetString(bytes[Bom.Length..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(Header, lines[0]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task Template_example_row_previews_without_errors()
    {
        var it = await ItAsync();
        using var template = await it.GetAsync(TemplatePath);
        var content = await template.Content.ReadAsByteArrayAsync(Ct);

        var job = await PreviewAsync(it, content);

        Assert.Equal(0, job.GetProperty("errorRows").GetInt32());
        Assert.Equal(1, job.GetProperty("validRows").GetInt32());
    }

    [Fact]
    public async Task Valid_file_previews_then_commits_every_row()
    {
        var it = await ItAsync();
        var content = Csv(
            "DT-1,laptop,Lenovo,T14,SN-1,Merkez Kampüs / A Blok,Muhasebe,in_stock,2027-12-31,2024-01-15,Yeni",
            "\"TB-1\",tablet,Apple,\"iPad, 10\",,Işık Binası,,maintenance,,,\"Ekran \"\"çizik\"\"\"");

        var job = await PreviewAsync(it, content);
        using var commit = await it.PostAsync(CommitPath(job));
        var committed = await it.ReadJsonAsync<JsonElement>(commit);

        Assert.Equal("previewed", job.GetProperty("status").GetString());
        Assert.Equal(2, job.GetProperty("totalRows").GetInt32());
        Assert.Equal(2, job.GetProperty("validRows").GetInt32());
        Assert.Empty(ErrorsOf(job));
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        Assert.Equal("committed", committed.GetProperty("status").GetString());
        await using var db = factory.Database.CreateDbContext();
        var tablet = await db.Assets.SingleAsync(a => a.AssetTag == "TB-1", Ct);
        Assert.Equal("iPad, 10", tablet.Model);
        Assert.Equal("Ekran \"çizik\"", tablet.Notes);
        Assert.Null(tablet.SerialNumber);
        Assert.Equal(2, await db.Assets.CountAsync(Ct));
        Assert.Equal(2, await db.AssetMovements.CountAsync(Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.AssetsImported, Ct));
    }

    [Fact]
    public async Task One_bad_row_blocks_the_commit_and_nothing_is_written()
    {
        var it = await ItAsync();
        var content = Csv(
            "DT-1,laptop,Lenovo,T14,,Işık Binası,,in_stock,,,",
            "DT-2,laptop,,T14,,Işık Binası,,in_stock,31.02.2025,,");

        var job = await PreviewAsync(it, content);
        using var commit = await it.PostAsync(CommitPath(job));

        Assert.Equal(1, job.GetProperty("errorRows").GetInt32());
        Assert.Equal(1, job.GetProperty("validRows").GetInt32());
        Assert.Contains((3, "brand", ErrorCodes.Required), ErrorsOf(job));
        Assert.Contains((3, "warrantyEndsOn", ErrorCodes.InvalidValue), ErrorsOf(job));
        Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode);
        Assert.Equal(ErrorCodes.ImportHasErrors, await ApiClient.ReadCodeAsync(commit));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(0, await db.Assets.CountAsync(Ct));
    }

    [Fact]
    public async Task Duplicates_in_the_file_and_in_the_database_are_reported()
    {
        var it = await ItAsync();
        var store = (await TestData.LocationAsync(factory, "Depo")).Id;
        await AssetCatalogueTests.CreateAsync(it, new { assetTag = "DT-1", type = "laptop", brand = "Lenovo", model = "T14", serialNumber = "SN-9", locationId = store });

        var job = await PreviewAsync(it, Csv(
            "dt-1,laptop,Lenovo,T14,,Depo,,in_stock,,,",
            "DT-2,laptop,Lenovo,T14,sn-9,Depo,,in_stock,,,",
            "DT-3,laptop,Lenovo,T14,SN-3,Depo,,in_stock,,,",
            "DT-3,laptop,Lenovo,T14,sn-3,Depo,,in_stock,,,"));

        var errors = ErrorsOf(job);
        Assert.Contains((2, "assetTag", ErrorCodes.AssetTagTaken), errors);
        Assert.Contains((3, "serialNumber", ErrorCodes.AssetSerialTaken), errors);
        Assert.Contains((5, "assetTag", ErrorCodes.ImportDuplicateInFile), errors);
        Assert.Contains((5, "serialNumber", ErrorCodes.ImportDuplicateInFile), errors);
        Assert.DoesNotContain(errors, e => e.Row == 4);
    }

    [Fact]
    public async Task Assigned_lost_and_unknown_values_are_refused()
    {
        var it = await ItAsync();

        var job = await PreviewAsync(it, Csv(
            "DT-1,laptop,Lenovo,T14,,Işık Binası,,assigned,,,",
            "DT-2,laptop,Lenovo,T14,,Işık Binası,,lost,,,",
            "DT-3,printer,Lenovo,T14,,Işık Binası,Yok,,,,"));

        var errors = ErrorsOf(job);
        Assert.Contains((2, "status", ErrorCodes.ImportStatusNotAllowed), errors);
        Assert.Contains((3, "status", ErrorCodes.ImportStatusNotAllowed), errors);
        Assert.Contains((4, "type", ErrorCodes.InvalidValue), errors);
        Assert.Contains((4, "department", ErrorCodes.ImportDepartmentNotFound), errors);
        Assert.DoesNotContain((4, "status", ErrorCodes.Required), errors);
    }

    [Fact]
    public async Task Location_path_matches_ignoring_turkish_case_and_refuses_ambiguous_or_inactive()
    {
        var it = await ItAsync();

        var job = await PreviewAsync(it, Csv(
            "DT-1,laptop,Lenovo,T14,,merkez kampüs / a blok,,in_stock,,,",
            "DT-2,laptop,Lenovo,T14,,IŞIK BİNASI,,in_stock,,,",
            "DT-3,laptop,Lenovo,T14,,A Blok,,in_stock,,,",
            "DT-4,laptop,Lenovo,T14,,Yok,,in_stock,,,",
            "DT-5,laptop,Lenovo,T14,,Eski Depo,,in_stock,,,"));

        var errors = ErrorsOf(job);
        Assert.DoesNotContain(errors, e => e.Row is 2 or 3);
        Assert.Contains((4, "location", ErrorCodes.ImportLocationAmbiguous), errors);
        Assert.Contains((5, "location", ErrorCodes.ImportLocationNotFound), errors);
        Assert.Contains((6, "location", ErrorCodes.ReferenceInactive), errors);
    }

    [Fact]
    public async Task Semicolon_file_with_turkish_dates_is_read()
    {
        var it = await ItAsync();
        var content = Encoding.UTF8.GetBytes(Header.Replace(',', ';') + "\nDT-1;laptop;Lenovo;T14;;Işık Binası;;in_stock;31.12.2027;15.01.2024;\n");

        var job = await PreviewAsync(it, content);

        Assert.Empty(ErrorsOf(job));
        Assert.Equal(1, job.GetProperty("validRows").GetInt32());
    }

    [Fact]
    public async Task Committing_again_returns_the_same_result_without_duplicates()
    {
        var it = await ItAsync();
        var job = await PreviewAsync(it, Csv("DT-1,laptop,Lenovo,T14,,Işık Binası,,in_stock,,,"));

        using var first = await it.PostAsync(CommitPath(job));
        using var second = await it.PostAsync(CommitPath(job));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(
            (await it.ReadJsonAsync<JsonElement>(first)).GetProperty("committedAt").GetDateTimeOffset(),
            (await it.ReadJsonAsync<JsonElement>(second)).GetProperty("committedAt").GetDateTimeOffset());
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.Assets.CountAsync(Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.AssetsImported, Ct));
    }

    [Fact]
    public async Task Asset_created_after_the_preview_makes_the_commit_fail_without_writing()
    {
        var it = await ItAsync();
        var store = (await TestData.LocationAsync(factory, "Depo")).Id;
        var job = await PreviewAsync(it, Csv("DT-1,laptop,Lenovo,T14,,Depo,,in_stock,,,", "DT-2,laptop,Lenovo,T14,,Depo,,in_stock,,,"));
        await AssetCatalogueTests.CreateAsync(it, new { assetTag = "DT-2", type = "laptop", brand = "Lenovo", model = "T14", locationId = store });

        using var commit = await it.PostAsync(CommitPath(job));

        Assert.Equal(HttpStatusCode.Conflict, commit.StatusCode);
        Assert.Equal(ErrorCodes.AssetTagTaken, await ApiClient.ReadCodeAsync(commit));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.Assets.CountAsync(Ct));
    }

    [Fact]
    public async Task Unknown_job_is_not_found()
    {
        var it = await ItAsync();

        using var commit = await it.PostAsync($"{ImportPath}/{Guid.CreateVersion7()}/commit");

        Assert.Equal(HttpStatusCode.NotFound, commit.StatusCode);
    }

    [Fact]
    public async Task Missing_header_is_refused()
    {
        var it = await ItAsync();

        using var response = await it.PostFileAsync(PreviewPath, FileName, Encoding.UTF8.GetBytes("a,b\n1,2"), CsvType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.ImportInvalidHeader, (await ApiClient.ReadFieldCodesAsync(response))["file"]);
    }

    [Fact]
    public async Task More_than_five_thousand_rows_are_refused()
    {
        var it = await ItAsync();
        var rows = Enumerable.Range(0, MaxRows + 1).Select(i => $"DT-{i},laptop,Lenovo,T14,,Işık Binası,,in_stock,,,").ToArray();

        using var response = await it.PostFileAsync(PreviewPath, FileName, Csv(rows), CsvType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.ImportTooManyRows, (await ApiClient.ReadFieldCodesAsync(response))["file"]);
    }

    [Fact]
    public async Task File_over_ten_megabytes_is_refused()
    {
        var it = await ItAsync();
        var content = new byte[MaxBytes + 1];
        Array.Fill(content, (byte)'x');

        using var response = await it.PostFileAsync(PreviewPath, FileName, content, CsvType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.ImportFileTooLarge, (await ApiClient.ReadFieldCodesAsync(response))["file"]);
    }
}
