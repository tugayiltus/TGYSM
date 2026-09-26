using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure.Files;
using Tgysm.Infrastructure.Persistence;
using Tgysm.Infrastructure.Security;

namespace Tgysm.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The connection string is read when the first DbContext is created, so hosts that never
        // touch the database (OpenAPI generation) start without one.
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured. Run tools/db-up.sh.");
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName);
        services.AddSingleton<IFileStore, LocalFileStore>();
        services.AddSingleton<IFileScanner, DevelopmentFileScanner>();
        return services;
    }
}
