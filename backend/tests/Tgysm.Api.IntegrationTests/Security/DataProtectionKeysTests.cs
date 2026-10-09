using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Tgysm.Infrastructure.Security;

namespace Tgysm.Api.IntegrationTests.Security;

/// <summary>
/// The API protects account-link secrets that the Worker unprotects, and a restored server must read what the old one
/// protected, so outside Development the key ring lives in one configured folder (ADR-0024).
/// </summary>
// Folder permissions are POSIX modes; the product and CI run on Linux and macOS hosts.
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DataProtectionKeysTests : IDisposable
{
    private const string Purpose = "tests.data-protection";
    private const string Secret = "set-password-token";
    private const UnixFileMode ReadAndExecuteOnly =
        UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tgysm-keys-" + Guid.NewGuid().ToString("N"));

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = nameof(DataProtectionKeysTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            File.SetUnixFileMode(_folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static ServiceProvider HostServices(string keysPath, string environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new Environment(environment));
        services.AddOptions<KeyStorageOptions>().Configure(o => o.KeysPath = keysPath);
        services.AddTgysmDataProtection();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void A_payload_protected_by_one_host_is_read_by_another_host_sharing_the_folder()
    {
        using var api = HostServices(_folder, Environments.Production);
        using var worker = HostServices(_folder, Environments.Production);

        var protectedValue = api.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect(Secret);

        Assert.Equal(Secret, worker.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(protectedValue));
        Assert.NotEmpty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void Outside_development_a_missing_folder_fails_with_the_key_name()
    {
        var error = Assert.Throws<InvalidOperationException>(() => KeyStorage.ResolveFolder(new KeyStorageOptions { KeysPath = " " }, new Environment(Environments.Production)));

        Assert.Contains(KeyStorageOptions.SectionName + ":" + nameof(KeyStorageOptions.KeysPath), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_the_host_cannot_write_fails_with_its_path()
    {
        Directory.CreateDirectory(_folder);
        File.SetUnixFileMode(_folder, ReadAndExecuteOnly);

        var error = Assert.Throws<InvalidOperationException>(() => KeyStorage.ResolveFolder(new KeyStorageOptions { KeysPath = _folder }, new Environment(Environments.Production)));

        Assert.Contains("Cannot write Data Protection keys to " + _folder, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Development_without_a_folder_keeps_the_default_key_ring()
    {
        Assert.Null(KeyStorage.ResolveFolder(new KeyStorageOptions(), new Environment(Environments.Development)));
    }
}
