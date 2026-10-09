using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tgysm.Infrastructure.Security;

public sealed class KeyStorageOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// Folder of the Data Protection key ring, shared by the API and the Worker and included in backups (ADR-0024).
    /// Required outside Development; empty in Development keeps the framework's per-user default.
    /// </summary>
    public string KeysPath { get; set; } = string.Empty;
}

/// <summary>Where the key ring lives, and the Data Protection set-up that uses it.</summary>
public static class KeyStorage
{
    /// <summary>
    /// The API protects the token and the Worker unprotects it; Data Protection mixes the host's content root into the
    /// purpose by default, so without a shared application name the Worker cannot read the API's secrets.
    /// </summary>
    public const string ApplicationName = "Tgysm";

    private const string ProbeFilePrefix = ".write-probe-";

    private static readonly string MissingFolderMessage =
        $"{KeyStorageOptions.SectionName}:{nameof(KeyStorageOptions.KeysPath)} must be set outside Development: the API and the Worker " +
        "must share one key-ring folder, and backups must include it.";

    /// <summary>The absolute key folder, or null for the Development default. Fails when the folder cannot be written.</summary>
    public static string? ResolveFolder(KeyStorageOptions options, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(options.KeysPath))
        {
            return environment.IsDevelopment() ? null : throw new InvalidOperationException(MissingFolderMessage);
        }

        var folder = Path.GetFullPath(options.KeysPath);
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, ProbeFilePrefix + Guid.NewGuid().ToString("N"));
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException($"Cannot write Data Protection keys to {folder}: {exception.Message}", exception);
        }

        return folder;
    }

    /// <summary>Reads <see cref="KeyStorageOptions"/> from the options system; the caller binds it to configuration.</summary>
    public static IServiceCollection AddTgysmDataProtection(this IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName(ApplicationName);
        services.AddOptions<KeyManagementOptions>()
            .Configure<IOptions<KeyStorageOptions>, IHostEnvironment, ILoggerFactory>((keyManagement, storage, environment, loggers) =>
            {
                if (ResolveFolder(storage.Value, environment) is { } folder)
                {
                    keyManagement.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(folder), loggers);
                }
            });
        services.AddHostedService<KeyStorageStartupCheck>();
        return services;
    }
}

/// <summary>Resolves the key folder at startup, so a missing or read-only folder stops the host before it serves anything.</summary>
public sealed class KeyStorageStartupCheck(IOptions<KeyStorageOptions> options, IHostEnvironment environment, ILogger<KeyStorageStartupCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var folder = KeyStorage.ResolveFolder(options.Value, environment);
        logger.LogInformation("Data Protection key folder: {KeyFolder}", folder ?? "framework default (Development)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
