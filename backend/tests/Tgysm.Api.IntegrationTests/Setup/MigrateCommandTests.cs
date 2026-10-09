using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Api.Cli;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Infrastructure;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Setup;

/// <summary>The <c>migrate</c> command runs against its own empty database, created and dropped by the test.</summary>
[Collection(ApiCollection.Name)]
public sealed class MigrateCommandTests(ApiFactory factory) : IAsyncLifetime
{
    private const int CliSuccess = 0;
    private const int CliFailure = 1;
    private const string AdminDatabase = "postgres";
    private const string LocalHost = "localhost";
    private const string UnreachableHost = "tgysm-no-such-host.invalid";

    private readonly string _databaseName = "tgysm_migrate_" + Guid.NewGuid().ToString("N");
    private string _adminConnectionString = string.Empty;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable(PostgresDatabase.AdminConnectionEnvironmentVariable)
            ?? new NpgsqlConnectionStringBuilder { Host = LocalHost, Database = AdminDatabase, Username = Environment.UserName }.ConnectionString;
        await ExecuteAdminAsync($"CREATE DATABASE \"{_databaseName}\"");
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    private string EmptyDatabaseConnection => new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }.ConnectionString;

    private IServiceProvider ServicesFor(string connectionString) =>
        factory.WithWebHostBuilder(b => b.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString)).Services;

    [Fact]
    public async Task Applies_every_migration_to_an_empty_database_and_then_reports_it_up_to_date()
    {
        var services = ServicesFor(EmptyDatabaseConnection);
        var first = new StringWriter();
        var second = new StringWriter();

        var firstExit = await MigrateCommand.RunAsync(services, [], first, TextWriter.Null);
        var secondExit = await MigrateCommand.RunAsync(services, [], second, TextWriter.Null);

        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(EmptyDatabaseConnection).UseSnakeCaseNamingConvention().Options);
        var applied = (await db.Database.GetAppliedMigrationsAsync(Ct)).ToList();
        Assert.Equal(CliSuccess, firstExit);
        Assert.Equal(CliSuccess, secondExit);
        Assert.Equal(db.Database.GetMigrations(), applied);
        Assert.Contains($"latest {applied[^1]}", first.ToString(), StringComparison.Ordinal);
        Assert.Contains("up to date", second.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_a_failure_when_the_database_cannot_be_reached()
    {
        var unreachable = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Host = UnreachableHost, Database = _databaseName, Timeout = 1 }.ConnectionString;
        var error = new StringWriter();

        var exit = await MigrateCommand.RunAsync(ServicesFor(unreachable), [], TextWriter.Null, error);

        Assert.Equal(CliFailure, exit);
        Assert.Contains("Migration failed", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prints_the_newest_migration_of_the_application_without_touching_the_database()
    {
        // Restore compares this with the archive's manifest (ADR-0025), before any database is reachable.
        var unreachable = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Host = UnreachableHost, Database = _databaseName, Timeout = 1 }.ConnectionString;
        var output = new StringWriter();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(EmptyDatabaseConnection).UseSnakeCaseNamingConvention().Options);

        var exit = await MigrateCommand.RunAsync(ServicesFor(unreachable), ["--print-latest"], output, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        Assert.Equal(db.Database.GetMigrations().Last(), output.ToString().Trim());
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
