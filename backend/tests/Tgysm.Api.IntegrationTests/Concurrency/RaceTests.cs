using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Concurrency;

/// <summary>Check-then-write rules must hold when two requests race (final review findings).</summary>
[Collection(ApiCollection.Name)]
public sealed class RaceTests(ApiFactory factory)
{
    private const int Rounds = 8;
    private const int ParallelFailedLogins = 12;
    private const string UsersPath = "/api/v1/admin/users";
    private const string LocationsPath = "/api/v1/admin/locations";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<uint> VersionOfAsync(ApiClient admin, string path)
    {
        using var response = await admin.GetAsync(path);
        return (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("version").GetUInt32();
    }

    [Fact]
    public async Task Two_admins_deactivating_each_other_at_once_keep_one_active_admin()
    {
        for (var round = 0; round < Rounds; round++)
        {
            await factory.ResetAsync();
            var (first, firstUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, $"first{round}@example.com");
            var (second, secondUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, $"second{round}@example.com");
            var secondVersion = await VersionOfAsync(first, $"{UsersPath}/{secondUser.Id}");
            var firstVersion = await VersionOfAsync(second, $"{UsersPath}/{firstUser.Id}");

            var responses = await Task.WhenAll(
                first.PostAsync($"{UsersPath}/{secondUser.Id}/deactivate", new { version = secondVersion }),
                second.PostAsync($"{UsersPath}/{firstUser.Id}/deactivate", new { version = firstVersion }));
            foreach (var response in responses)
            {
                response.Dispose();
            }

            await using var db = factory.Database.CreateDbContext();
            Assert.True(await db.Users.AnyAsync(u => u.IsActive && u.Role == UserRole.Admin, Ct), $"No active admin left in round {round}.");
        }
    }

    [Fact]
    public async Task Crossed_location_moves_at_once_never_create_a_cycle()
    {
        for (var round = 0; round < Rounds; round++)
        {
            await factory.ResetAsync();
            var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, $"admin{round}@example.com");
            using var createdA = await admin.PostAsync(LocationsPath, new { name = "A" });
            using var createdB = await admin.PostAsync(LocationsPath, new { name = "B" });
            var a = await admin.ReadJsonAsync<JsonElement>(createdA);
            var b = await admin.ReadJsonAsync<JsonElement>(createdB);
            var aId = a.GetProperty("id").GetGuid();
            var bId = b.GetProperty("id").GetGuid();

            var responses = await Task.WhenAll(
                admin.PutAsync($"{LocationsPath}/{aId}", new { version = a.GetProperty("version").GetUInt32(), name = "A", parentId = bId }),
                admin.PutAsync($"{LocationsPath}/{bId}", new { version = b.GetProperty("version").GetUInt32(), name = "B", parentId = aId }));
            foreach (var response in responses)
            {
                response.Dispose();
            }

            await using var db = factory.Database.CreateDbContext();
            var parents = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.ParentId, Ct);
            Assert.False(parents[aId] == bId && parents[bId] == aId, $"Cycle A<->B created in round {round}.");
        }
    }

    [Fact]
    public async Task Parallel_wrong_passwords_still_lock_the_account()
    {
        await factory.ResetAsync();
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, "burst@example.com");
        var client = ApiClient.Create(factory);
        await client.RefreshCsrfAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, ParallelFailedLogins)
            .Select(_ => client.PostAsync(ApiClient.LoginPath, new { email = "burst@example.com", password = "Wrong-password-1" })));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var db = factory.Database.CreateDbContext();
        var lockoutEnd = await db.Users.Where(u => u.Id == user.Id).Select(u => u.LockoutEnd).SingleAsync(Ct);
        Assert.NotNull(lockoutEnd);
    }
}
