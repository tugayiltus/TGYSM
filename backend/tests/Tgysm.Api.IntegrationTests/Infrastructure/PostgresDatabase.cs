using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>Creates a throw-away database on local PostgreSQL 17, applies migrations, drops it at the end.</summary>
public sealed class PostgresDatabase : IAsyncLifetime
{
    public const string AdminConnectionEnvironmentVariable = "TGYSM_TEST_ADMIN_CONNECTION";
    private const string DatabaseNamePrefix = "tgysm_test_";
    private const string AdminDatabase = "postgres";
    private const string LocalHost = "localhost";

    // replica mode skips the append-only triggers; only the test fixture may wipe audit rows.
    private const string TruncateAllSql =
        "SET session_replication_role = replica; "
        + "TRUNCATE email_deliveries, notification_outbox, ticket_assets, ticket_relations, ticket_events, ticket_attachments, ticket_comments, ticket_assignments, approvals, tickets, ticket_counters, request_types, categories, import_jobs, asset_movements, asset_assignments, assets, team_memberships, teams, audit_events, users, departments, locations RESTART IDENTITY CASCADE; "
        + "SET session_replication_role = origin;";

    private readonly string _databaseName = DatabaseNamePrefix + Guid.NewGuid().ToString("N");
    private string _adminConnectionString = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable(AdminConnectionEnvironmentVariable)
            ?? new NpgsqlConnectionStringBuilder { Host = LocalHost, Database = AdminDatabase, Username = Environment.UserName }.ConnectionString;

        await ExecuteAdminAsync($"CREATE DATABASE \"{_databaseName}\"");
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = _databaseName }.ConnectionString;

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(TruncateAllSql);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)");
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
