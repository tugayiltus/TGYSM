using Microsoft.Extensions.DependencyInjection;
using Tgysm.Domain.Organization;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>Seeds reference data directly through the DbContext.</summary>
public static class TestData
{
    public static async Task<Location> LocationAsync(ApiFactory factory, string name, Guid? parentId = null, bool active = true)
    {
        var location = Location.Create(name, parentId);
        if (!active)
        {
            location.Deactivate();
        }

        await SaveAsync(factory, db => db.Locations.Add(location));
        return location;
    }

    public static async Task<Department> DepartmentAsync(ApiFactory factory, string name, Guid? approverUserId = null)
    {
        var department = Department.Create(name, approverUserId);
        await SaveAsync(factory, db => db.Departments.Add(department));
        return department;
    }

    public static async Task SaveAsync(ApiFactory factory, Action<AppDbContext> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
