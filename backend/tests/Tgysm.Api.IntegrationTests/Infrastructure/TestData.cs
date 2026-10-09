using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Catalog;
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

    public static async Task<Team> TeamAsync(ApiFactory factory, string name, bool defaultIntake = false, params Guid[] memberIds)
    {
        var team = Team.Create(name);
        if (defaultIntake)
        {
            team.MarkDefaultIntake();
        }

        await SaveAsync(factory, db =>
        {
            db.Teams.Add(team);
            db.TeamMemberships.AddRange(memberIds.Select(id => TeamMembership.Create(team.Id, id, DateTimeOffset.UtcNow)));
        });
        return team;
    }

    public static async Task<RequestType> RequestTypeAsync(ApiFactory factory, string name, bool requiresApproval)
    {
        var type = RequestType.Create(name, requiresApproval);
        await SaveAsync(factory, db => db.RequestTypes.Add(type));
        return type;
    }

    public static async Task<Category> CategoryAsync(ApiFactory factory, string name, CategoryScope scope = CategoryScope.Both, bool active = true)
    {
        var category = Category.Create(name, null, scope);
        if (!active)
        {
            category.Deactivate();
        }

        await SaveAsync(factory, db => db.Categories.Add(category));
        return category;
    }

    /// <summary>The catalog model for a brand, model name and device type, created on first use (ADR-0023).</summary>
    public static async Task<Guid> ModelAsync(ApiFactory factory, string brandName = "Lenovo", string modelName = "T14", AssetType type = AssetType.Laptop, bool active = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;
        var brandKey = CatalogName.Normalize(brandName);
        var brand = await db.Brands.SingleOrDefaultAsync(b => b.NormalizedName == brandKey, ct);
        if (brand is null)
        {
            brand = Brand.Create(brandName, DateTimeOffset.UtcNow);
            db.Brands.Add(brand);
        }

        var modelKey = CatalogName.Normalize(modelName);
        var model = await db.AssetModels.SingleOrDefaultAsync(m => m.BrandId == brand.Id && m.Type == type && m.NormalizedName == modelKey, ct);
        if (model is null)
        {
            model = AssetModel.Create(brand.Id, type, modelName, DateTimeOffset.UtcNow);
            db.AssetModels.Add(model);
        }

        if (!active)
        {
            model.Deactivate(DateTimeOffset.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        return model.Id;
    }

    public static async Task SaveAsync(ApiFactory factory, Action<AppDbContext> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
