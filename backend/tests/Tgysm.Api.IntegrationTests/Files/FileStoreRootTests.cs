using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tgysm.Infrastructure.Files;

namespace Tgysm.Api.IntegrationTests.Files;

/// <summary>
/// The API stores attachments and the Worker stores export files that the API serves, so both hosts must resolve one file-store
/// root: it is configured explicitly outside Development, and each host logs the root it resolved at startup.
/// </summary>
public sealed class FileStoreRootTests
{
    private const string RelativeRoot = "some/relative/files";

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = nameof(FileStoreRootTests);

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static LocalFileStore StoreOf(string rootPath, string environment) =>
        new(Options.Create(new FileStorageOptions { RootPath = rootPath }), new Environment(environment));

    [Fact]
    public void A_configured_root_is_used_as_a_full_path_in_every_environment()
    {
        Assert.Equal(Path.GetFullPath(RelativeRoot), StoreOf(RelativeRoot, Environments.Production).Root);
        Assert.Equal(Path.GetFullPath(RelativeRoot), StoreOf(RelativeRoot, Environments.Development).Root);
    }

    [Fact]
    public void Development_without_a_root_uses_the_per_user_folder_both_hosts_share()
    {
        var root = StoreOf(string.Empty, Environments.Development).Root;

        Assert.True(Path.IsPathRooted(root));
        Assert.StartsWith(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), root, StringComparison.Ordinal);
    }

    [Fact]
    public void Outside_development_a_missing_root_fails_with_a_clear_message()
    {
        var error = Assert.Throws<InvalidOperationException>(() => StoreOf(" ", Environments.Production));

        Assert.Contains(FileStorageOptions.SectionName + ":" + nameof(FileStorageOptions.RootPath), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_startup_log_names_the_resolved_root_once()
    {
        var store = StoreOf(RelativeRoot, Environments.Production);
        var logger = new CapturingLogger<FileStoreRootLog>();

        await new FileStoreRootLog(store, logger).StartAsync(TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(store.Root, entry.Message, StringComparison.Ordinal);
    }
}
