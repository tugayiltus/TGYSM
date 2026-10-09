using Microsoft.EntityFrameworkCore;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.Cli;

/// <summary>
/// <c>Tgysm.Api migrate</c>: applies pending EF Core migrations and exits. The deployment runs it once before the API
/// and the Worker start, so the images need no EF tooling. <c>migrate --print-latest</c> prints the newest migration
/// this build knows, without a database (restore refuses archives from a newer build, ADR-0025).
/// </summary>
internal static class MigrateCommand
{
    public const string Name = "migrate";

    public const string PrintLatestOption = "--print-latest";

    private const int Success = 0;
    private const int Failure = 1;

    public static async Task<int> RunAsync(IServiceProvider services, IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (args is [PrintLatestOption])
        {
            await output.WriteLineAsync(db.Database.GetMigrations().Last());
            return Success;
        }

        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            await db.Database.MigrateAsync();
            var latest = (await db.Database.GetAppliedMigrationsAsync()).LastOrDefault() ?? "none";
            await output.WriteLineAsync(pending.Count == 0
                ? $"Database is up to date ({latest})."
                : $"Applied {pending.Count} migration(s); latest {latest}.");
            return Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await error.WriteLineAsync($"Migration failed: {exception.Message}");
            return Failure;
        }
    }
}
