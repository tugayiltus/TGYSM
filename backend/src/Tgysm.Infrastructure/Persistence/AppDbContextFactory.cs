using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>Used only by dotnet-ef (migrations add / database update).</summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string ConnectionEnvironmentVariable = "TGYSM_CONNECTION_STRING";

    // "migrations add" never connects; this value only satisfies the provider.
    private const string DesignTimeFallbackConnection = "Host=localhost;Database=tgysm_design;Username=tgysm";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable) ?? DesignTimeFallbackConnection;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
