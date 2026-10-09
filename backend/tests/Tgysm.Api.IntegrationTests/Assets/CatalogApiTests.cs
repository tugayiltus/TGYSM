using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Assets;

[Collection(ApiCollection.Name)]
public sealed class CatalogApiTests(ApiFactory factory) : IAsyncLifetime
{
    private const string BrandsPath = "/api/v1/admin/brands";
    private const string ModelsPath = "/api/v1/admin/models";
    private const string CatalogPath = "/api/v1/assets/catalog";
    private Guid _storeId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _storeId = (await TestData.LocationAsync(factory, "Depo")).Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<(ApiClient Client, Domain.Users.User User)> AdminAsync() => ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin2@example.com");

    private static async Task<JsonElement> CreatedAsync(ApiClient client, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Brand_names_differing_only_in_case_or_spacing_are_duplicates()
    {
        var (admin, _) = await AdminAsync();
        using var first = await admin.PostAsync(BrandsPath, new { name = "Lenovo" });
        await CreatedAsync(admin, first);

        using var second = await admin.PostAsync(BrandsPath, new { name = "  LENOVO " });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.AssetCatalogDuplicate, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task A_model_name_is_unique_per_brand_and_type_and_free_for_another_type()
    {
        var (admin, _) = await AdminAsync();
        using var brandResponse = await admin.PostAsync(BrandsPath, new { name = "Lenovo" });
        var brandId = (await CreatedAsync(admin, brandResponse)).GetProperty("id").GetGuid();

        using var laptop = await admin.PostAsync(ModelsPath, new { brandId, type = "laptop", name = "ThinkPad T14" });
        await CreatedAsync(admin, laptop);
        using var duplicate = await admin.PostAsync(ModelsPath, new { brandId, type = "laptop", name = "thinkpad  t14" });
        using var variant = await admin.PostAsync(ModelsPath, new { brandId, type = "laptop", name = "ThinkPad T14s" });
        using var otherType = await admin.PostAsync(ModelsPath, new { brandId, type = "desktop", name = "ThinkPad T14" });

        Assert.Equal(ErrorCodes.AssetCatalogDuplicate, await ApiClient.ReadCodeAsync(duplicate));
        await CreatedAsync(admin, variant);
        await CreatedAsync(admin, otherType);
    }

    [Fact]
    public async Task Creating_an_asset_with_an_inactive_model_is_refused()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        var inactive = await TestData.ModelAsync(factory, "Dell", "Latitude", active: false);

        using var response = await it.PostAsync(AssetCatalogueTests.AssetsPath, new { assetTag = "DT-1", modelId = inactive, locationId = _storeId });

        Assert.Equal(ErrorCodes.AssetModelInactive, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task An_asset_whose_model_was_deactivated_later_can_still_be_edited_and_keeps_its_model()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        var modelId = await TestData.ModelAsync(factory, "Dell", "Latitude");
        var asset = await AssetCatalogueTests.CreateAsync(it, new { assetTag = "DT-1", modelId, locationId = _storeId });
        await TestData.ModelAsync(factory, "Dell", "Latitude", active: false);

        using var update = await it.PutAsync($"{AssetCatalogueTests.AssetsPath}/{asset.GetProperty("id").GetGuid()}",
            new { version = asset.GetProperty("version").GetUInt32(), assetTag = "DT-1", modelId, locationId = _storeId, notes = "Yeni not" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await it.ReadJsonAsync<JsonElement>(update);
        Assert.Equal((modelId, "Dell", "Latitude", "Yeni not"),
            (updated.GetProperty("modelId").GetGuid(), updated.GetProperty("brand").GetString(), updated.GetProperty("model").GetString(), updated.GetProperty("notes").GetString()));
    }

    [Fact]
    public async Task An_asset_takes_its_type_and_names_from_the_model()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        var modelId = await TestData.ModelAsync(factory, "Apple", "iPhone 15", AssetType.Phone);

        var asset = await AssetCatalogueTests.CreateAsync(it, new { assetTag = "PH-1", modelId, locationId = _storeId });

        Assert.Equal(("phone", "Apple", "iPhone 15"), (asset.GetProperty("type").GetString(), asset.GetProperty("brand").GetString(), asset.GetProperty("model").GetString()));
    }

    [Fact]
    public async Task Renaming_a_brand_or_model_updates_the_names_shown_on_its_assets()
    {
        var (admin, _) = await AdminAsync();
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        var modelId = await TestData.ModelAsync(factory, "Lenvo", "T14");
        var asset = await AssetCatalogueTests.CreateAsync(it, new { assetTag = "DT-1", modelId, locationId = _storeId });
        await using (var db = factory.Database.CreateDbContext())
        {
            var model = await db.AssetModels.SingleAsync(m => m.Id == modelId, Ct);
            var brand = await db.Brands.SingleAsync(b => b.Id == model.BrandId, Ct);
            using var brandRename = await admin.PutAsync($"{BrandsPath}/{brand.Id}", new { version = brand.Version, name = "Lenovo" });
            using var modelRename = await admin.PutAsync($"{ModelsPath}/{model.Id}", new { version = model.Version, name = "ThinkPad T14" });
            Assert.Equal(HttpStatusCode.OK, brandRename.StatusCode);
            Assert.Equal(HttpStatusCode.OK, modelRename.StatusCode);
        }

        using var reread = await it.GetAsync($"{AssetCatalogueTests.AssetsPath}/{asset.GetProperty("id").GetGuid()}");
        var item = await it.ReadJsonAsync<JsonElement>(reread);
        Assert.Equal(("Lenovo", "ThinkPad T14"), (item.GetProperty("brand").GetString(), item.GetProperty("model").GetString()));
    }

    [Fact]
    public async Task The_asset_form_catalog_offers_only_active_brands_and_models()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        await TestData.ModelAsync(factory, "Lenovo", "T14");
        await TestData.ModelAsync(factory, "Lenovo", "T13", active: false);

        using var response = await it.GetAsync(CatalogPath);
        var brands = await it.ReadJsonAsync<JsonElement>(response);

        var lenovo = Assert.Single(brands.EnumerateArray());
        Assert.Equal(["T14"], lenovo.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("name").GetString()).ToArray());
    }

    [Fact]
    public async Task Catalog_admin_is_for_admins_and_the_form_catalog_for_IT_staff()
    {
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "emp@example.com");
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var employeeCatalog = await employee.GetAsync(CatalogPath);
        using var itBrands = await it.PostAsync(BrandsPath, new { name = "HP" });

        Assert.Equal(HttpStatusCode.Forbidden, employeeCatalog.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, itBrands.StatusCode);
    }

    [Fact]
    public async Task Catalog_changes_are_audited()
    {
        var (admin, _) = await AdminAsync();
        using var created = await admin.PostAsync(BrandsPath, new { name = "HP" });
        var brand = await CreatedAsync(admin, created);
        using var _ = await admin.PostAsync($"{BrandsPath}/{brand.GetProperty("id").GetGuid()}/deactivate", new { version = brand.GetProperty("version").GetUInt32() });

        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents.Where(e => e.EntityType == AuditEntityTypes.Brand).Select(e => e.Action).ToListAsync(Ct);
        Assert.Equal(2, actions.Count);
        Assert.Contains(AuditActions.BrandCreated, actions);
        Assert.Contains(AuditActions.BrandDeactivated, actions);
    }
}
