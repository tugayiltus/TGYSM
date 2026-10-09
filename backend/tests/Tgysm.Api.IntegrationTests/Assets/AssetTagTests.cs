using System.Net;
using System.Text;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

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

        Assert.Equal(("DMR-000001", "DMR-000002"), (first.GetProperty("assetTag").GetString(), second.GetProperty("assetTag").GetString()));
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

        Assert.Equal("DMR-000001", (await it.ReadJsonAsync<JsonElement>(update)).GetProperty("assetTag").GetString());
    }

    [Fact]
    public async Task Csv_rows_without_a_tag_get_one_at_commit_and_the_preview_uses_no_numbers()
    {
        var (it, _) = await ItAsync();
        var job = await PreviewAsync(it, ",laptop,Lenovo,T14,,Depo,,in_stock,,,", "ESKI-1,laptop,Lenovo,T14,,Depo,,in_stock,,,");
        Assert.Equal(2, job.GetProperty("validRows").GetInt32());
        var beforeCommit = await AssetCatalogueTests.CreateAsync(it, new { modelId = _modelId, locationId = _storeId });
        Assert.Equal("DMR-000001", beforeCommit.GetProperty("assetTag").GetString());

        using var commit = await it.PostAsync($"/api/v1/assets/import/{job.GetProperty("jobId").GetGuid()}/commit", new { });
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);

        using var list = await it.GetAsync($"{AssetCatalogueTests.AssetsPath}?pageSize=10");
        var tags = (await it.ReadJsonAsync<JsonElement>(list)).GetProperty("items").EnumerateArray().Select(a => a.GetProperty("assetTag").GetString()).Order().ToArray();
        Assert.Equal(["DMR-000001", "DMR-000002", "ESKI-1"], tags);
    }

    [Fact]
    public async Task A_csv_tag_with_the_generated_prefix_is_refused()
    {
        var (it, _) = await ItAsync();

        var job = await PreviewAsync(it, "dmr-000123,laptop,Lenovo,T14,,Depo,,in_stock,,,");

        var error = Assert.Single(job.GetProperty("errors").EnumerateArray());
        Assert.Equal(("assetTag", ErrorCodes.ImportReservedTag), (error.GetProperty("field").GetString(), error.GetProperty("code").GetString()));
    }
}
