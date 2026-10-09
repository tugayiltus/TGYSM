using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure.Email;
using Tgysm.Infrastructure.Files;
using Tgysm.Infrastructure.Persistence;
using Tgysm.Infrastructure.Reports;
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
        services.AddScoped<IAssetTagGenerator, AssetTagGenerator>();
        services.AddScoped<IOutboxClaimer, OutboxClaimer>();
        services.AddScoped<IEmailDeliveryClaimer, EmailDeliveryClaimer>();
        services.AddScoped<IExportJobClaimer, ExportJobClaimer>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName);
        services.AddSingleton<LocalFileStore>();
        services.AddSingleton<IFileStore>(provider => provider.GetRequiredService<LocalFileStore>());
        services.AddSingleton<IFileScanner, DevelopmentFileScanner>();
        services.AddScoped<IReportFileWriter, ExcelReportWriter>();

        // Account-link tokens (ADR-0015, ADR-0017); the key ring is shared by the API and the Worker (ADR-0024).
        services.AddOptions<KeyStorageOptions>().BindConfiguration(KeyStorageOptions.SectionName);
        services.AddTgysmDataProtection();
        services.AddSingleton<IAccountLinkProtector, AccountLinkProtector>();

        // Email:Mode picks the transport (spec §3.5); resolving IEmailSender with a missing/unrecognized Mode
        // throws immediately ("fail fast when missing"). Tgysm.Worker additionally marks EmailOptions
        // ValidateOnStart so a bad Mode fails the host at startup, not at the first send.
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SectionName);
        services.AddScoped<IEmailSender>(provider =>
        {
            var mode = provider.GetRequiredService<IOptionsMonitor<EmailOptions>>().CurrentValue.Mode;
            return mode switch
            {
                EmailModes.Smtp => ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider),
                EmailModes.Pickup => ActivatorUtilities.CreateInstance<PickupFolderEmailSender>(provider),
                _ => throw new InvalidOperationException(
                    $"Email:Mode '{mode}' is not supported; expected '{EmailModes.Smtp}' or '{EmailModes.Pickup}'."),
            };
        });
        return services;
    }
}
