namespace Tgysm.Infrastructure.Persistence.PostMigration;

/// <summary>A one-time data fix that needs application code; runs after migrations, inside the runner's transaction.</summary>
public interface IPostMigrationStep
{
    /// <summary>Stable id recorded in <c>maintenance_steps</c>; never reuse or rename one that shipped.</summary>
    string Id { get; }

    Task RunAsync(AppDbContext db, CancellationToken cancellationToken);
}
