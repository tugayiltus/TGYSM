using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure.Email;
using Tgysm.Infrastructure.Files;
using Tgysm.Infrastructure.Persistence;
using Tgysm.Infrastructure.Security;

namespace Tgysm.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";
    private const string DataProtectionApplicationName = "Tgysm";

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
        services.AddScoped<IOutboxClaimer, OutboxClaimer>();
        services.AddScoped<IEmailDeliveryClaimer, EmailDeliveryClaimer>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName);
        services.AddSingleton<IFileStore, LocalFileStore>();
        services.AddSingleton<IFileScanner, DevelopmentFileScanner>();

        // Account-link tokens (ADR-0015, ADR-0017); key ring persistence is unconfigured for now (flagged as a
        // production risk in ADR-0004) and out of this task's scope.
        // The API protects the token and the Worker unprotects it; Data Protection mixes the host's content root into
        // the purpose by default, so without a shared application name the Worker cannot read the API's secrets.
        services.AddDataProtection().SetApplicationName(DataProtectionApplicationName);
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
