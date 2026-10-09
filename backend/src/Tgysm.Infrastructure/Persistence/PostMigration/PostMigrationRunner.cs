using Microsoft.EntityFrameworkCore;

namespace Tgysm.Infrastructure.Persistence.PostMigration;

/// <summary>
/// Runs every <see cref="IPostMigrationStep"/> not yet recorded in <c>maintenance_steps</c>, each in its own transaction
/// together with its record, so a failed step changes nothing and runs again next time. Called by the <c>migrate</c>
/// command and by the test database set-up.
/// </summary>
public static class PostMigrationRunner
{
    /// <summary>All steps, in the order they run.</summary>
    public static IReadOnlyList<IPostMigrationStep> Steps { get; } = [new RecomputeCatalogNamesStep()];

    public static async Task<IReadOnlyList<string>> RunPendingAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var done = await db.Database.SqlQueryRaw<string>("SELECT id AS \"Value\" FROM maintenance_steps").ToListAsync(cancellationToken);
        var ran = new List<string>();
        foreach (var step in Steps.Where(s => !done.Contains(s.Id)))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await step.RunAsync(db, cancellationToken);
            await db.Database.ExecuteSqlAsync($"INSERT INTO maintenance_steps (id, completed_at) VALUES ({step.Id}, now())", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            ran.Add(step.Id);
        }

        return ran;
    }
}
