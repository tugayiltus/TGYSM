using System.Net;
using System.Text;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Assets;

[Collection(ApiCollection.Name)]
public sealed class AssetTagTests(ApiFactory factory) : IAsyncLifetime
{
    private const int ParallelCreates = 20;
    private const string Header = "Demirbaş No,Tür,Marka,Model,Seri No,Konum,Departman,Durum,Garanti Bitiş,Satın Alma,Notlar";
    private Guid _storeId;
    private Guid _modelId;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _storeId = (await TestData.LocationAsync(factory, "Depo")).Id;
        _modelId = await TestData.ModelAsync(factory);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Tags carry the company calendar year (Europe/Istanbul in tests) of the moment they are created.</summary>
    private static string Tag(int number) =>
        AssetTagFormat.Format(AssetTagFormat.YearOf(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(AppOptions.DefaultTimeZone)), number);

    private Task<(ApiClient Client, User User)> ItAsync() => ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

    private static async Task<JsonElement> PreviewAsync(ApiClient client, params string[] rows)
    {
        using var response = await client.PostFileAsync("/api/v1/assets/import/preview", "cihazlar.csv", Encoding.UTF8.GetBytes(string.Join("\r\n", [Header, .. rows])), "text/csv");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task A_new_asset_gets_the_next_generated_tag_whatever_the_client_sends()
    {
        var (it, _) = await ItAsync();
        var first = await AssetCatalogueTests.CreateAsync(it, new { assetTag = "BENIM-ETIKET", modelId = _modelId, locationId = _storeId });
        var second = await AssetCatalogueTests.CreateAsync(it, new { modelId = _modelId, locationId = _storeId });

        Assert.Equal((Tag(1), Tag(2)), (first.GetProperty("assetTag").GetString(), second.GetProperty("assetTag").GetString()));
    }

    [Fact]
    public async Task A_number_already_held_by_an_existing_tag_is_skipped()
    {
        // Imported or older data may already hold a number in the generated format; the counter skips it.
        await TestData.LegacyAssetAsync(factory, Tag(1), _storeId);
        var (it, _) = await ItAsync();

        var created = await AssetCatalogueTests.CreateAsync(it, new { modelId = _modelId, locationId = _storeId });

        Assert.Equal(Tag(2), created.GetProperty("assetTag").GetString());
    }

    [Fact]
    public async Task Each_year_counts_from_one()
    {
        await using var db = factory.Database.CreateDbContext();
        var generator = new AssetTagGenerator(db);

        var first = await generator.NextAsync(2031, TestContext.Current.CancellationToken);
        var second = await generator.NextAsync(2031, TestContext.Current.CancellationToken);
        var nextYear = await generator.NextAsync(2032, TestContext.Current.CancellationToken);

        Assert.Equal(["ASSET-2031-0001", "ASSET-2031-0002", "ASSET-2032-0001"], new[] { first, second, nextYear });
    }

    [Fact]
    public async Task Parallel_creates_get_distinct_tags()
    {
        var (it, _) = await ItAsync();
        var created = await Task.WhenAll(Enumerable.Range(0, ParallelCreates).Select(_ => AssetCatalogueTests.CreateAsync(it, new { modelId = _modelId, locationId = _storeId })));

        Assert.Equal(ParallelCreates, created.Select(a => a.GetProperty("assetTag").GetString()).Distinct().Count());
    }

    [Fact]
    public async Task Editing_never_changes_the_tag()
    {
        var (it, _) = await ItAsync();
        var asset = await AssetCatalogueTests.CreateAsync(it, new { modelId = _modelId, locationId = _storeId });

        using var update = await it.PutAsync($"{AssetCatalogueTests.AssetsPath}/{asset.GetProperty("id").GetGuid()}",
            new { version = asset.GetProperty("version").GetUInt32(), assetTag = "DEGISTI", modelId = _modelId, locationId = _storeId });

        Assert.Equal(Tag(1), (await it.ReadJsonAsync<JsonElement>(update)).GetProperty("assetTag").GetString());
    }

    [Fact]
    public async Task Every_csv_row_gets_a_generated_tag_at_commit_and_the_preview_uses_no_numbers()
    {
        var (it, _) = await ItAsync();
        var job = await PreviewAsync(it, ",laptop,Lenovo,T14,,Depo,,in_stock,,,", "ESKI-1,laptop,Lenovo,T14,,Depo,,in_stock,,,");
        Assert.Equal(2, job.GetProperty("validRows").GetInt32());
        var beforeCommit = await AssetCatalogueTests.CreateAsync(it, new { modelId = _modelId, locationId = _storeId });
        Assert.Equal(Tag(1), beforeCommit.GetProperty("assetTag").GetString());

        using var commit = await it.PostAsync($"/api/v1/assets/import/{job.GetProperty("jobId").GetGuid()}/commit", new { });
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);

        using var list = await it.GetAsync($"{AssetCatalogueTests.AssetsPath}?pageSize=10");
        var tags = (await it.ReadJsonAsync<JsonElement>(list)).GetProperty("items").EnumerateArray().Select(a => a.GetProperty("assetTag").GetString()).Order().ToArray();
        Assert.Equal([Tag(1), Tag(2), Tag(3)], tags);
    }

    [Fact]
    public async Task A_filled_tag_cell_from_an_old_file_is_ignored()
    {
        var (it, _) = await ItAsync();

        var job = await PreviewAsync(it, "asset-2026-0123,laptop,Lenovo,T14,,Depo,,in_stock,,,", "dmrbs555555,laptop,Lenovo,T14,,Depo,,in_stock,,,");

        Assert.Empty(job.GetProperty("errors").EnumerateArray());
        Assert.Equal(2, job.GetProperty("validRows").GetInt32());
    }
}
