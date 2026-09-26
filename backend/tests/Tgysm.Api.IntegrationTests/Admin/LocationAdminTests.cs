using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class LocationAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string LocationsPath = "/api/v1/admin/locations";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static async Task<JsonElement> CreateAsync(ApiClient admin, string name, Guid? parentId = null)
    {
        using var response = await admin.PostAsync(LocationsPath, new { name, parentId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    private static Guid Id(JsonElement item) => item.GetProperty("id").GetGuid();

    private static uint Version(JsonElement item) => item.GetProperty("version").GetUInt32();

    [Fact]
    public async Task Builds_a_tree_and_lists_all_locations()
    {
        var admin = await AdminAsync();
        var campus = await CreateAsync(admin, "Merkez Kampüs");
        await CreateAsync(admin, "A Blok", Id(campus));

        using var response = await admin.GetAsync(LocationsPath);
        var items = await admin.ReadJsonAsync<JsonElement>(response);

        Assert.Equal(2, items.GetArrayLength());
        Assert.Contains(items.EnumerateArray(), i => i.GetProperty("parentId").ValueKind == JsonValueKind.Null);
        Assert.Contains(items.EnumerateArray(), i => i.GetProperty("parentId").ValueKind != JsonValueKind.Null && i.GetProperty("parentId").GetGuid() == Id(campus));
    }

    [Fact]
    public async Task Same_root_name_is_rejected_but_same_name_under_other_parents_is_allowed()
    {
        var admin = await AdminAsync();
        var first = await CreateAsync(admin, "Kampüs A");
        var second = await CreateAsync(admin, "Kampüs B");
        await CreateAsync(admin, "Bina 1", Id(first));
        await CreateAsync(admin, "Bina 1", Id(second));

        using var duplicate = await admin.PostAsync(LocationsPath, new { name = "KAMPÜS A" });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ErrorCodes.LocationNameTaken, await ApiClient.ReadCodeAsync(duplicate));
    }

    [Fact]
    public async Task Moving_a_location_under_itself_or_its_descendant_is_rejected()
    {
        var admin = await AdminAsync();
        var campus = await CreateAsync(admin, "Kampüs");
        var building = await CreateAsync(admin, "Bina", Id(campus));
        var floor = await CreateAsync(admin, "Kat 1", Id(building));

        using var underDescendant = await admin.PutAsync($"{LocationsPath}/{Id(campus)}", new { version = Version(campus), name = "Kampüs", parentId = Id(floor) });
        using var underSelf = await admin.PutAsync($"{LocationsPath}/{Id(campus)}", new { version = Version(campus), name = "Kampüs", parentId = Id(campus) });

        Assert.Equal(ErrorCodes.LocationCycle, await ApiClient.ReadCodeAsync(underDescendant));
        Assert.Equal(ErrorCodes.LocationCycle, await ApiClient.ReadCodeAsync(underSelf));
    }

    [Fact]
    public async Task Parent_with_active_children_cannot_be_deactivated_and_child_of_inactive_parent_cannot_be_reactivated()
    {
        var admin = await AdminAsync();
        var campus = await CreateAsync(admin, "Kampüs");
        var building = await CreateAsync(admin, "Bina", Id(campus));

        using var blocked = await admin.PostAsync($"{LocationsPath}/{Id(campus)}/deactivate", new { version = Version(campus) });
        Assert.Equal(ErrorCodes.LocationHasActiveChildren, await ApiClient.ReadCodeAsync(blocked));

        using var childOff = await admin.PostAsync($"{LocationsPath}/{Id(building)}/deactivate", new { version = Version(building) });
        var inactiveChild = await admin.ReadJsonAsync<JsonElement>(childOff);
        using var parentOff = await admin.PostAsync($"{LocationsPath}/{Id(campus)}/deactivate", new { version = Version(campus) });
        Assert.Equal(HttpStatusCode.OK, parentOff.StatusCode);

        using var childOn = await admin.PostAsync($"{LocationsPath}/{Id(building)}/reactivate", new { version = Version(inactiveChild) });
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(childOn));

        using var newChild = await admin.PostAsync(LocationsPath, new { name = "Yeni Bina", parentId = Id(campus) });
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(newChild));
    }
}
