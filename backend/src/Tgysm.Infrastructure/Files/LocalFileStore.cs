using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;

namespace Tgysm.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "Files";

    /// <summary>
    /// Folder for attachments and export files. The API and the Worker must use the same folder (the Worker writes export files the
    /// API serves), so it is required outside Development. Empty in Development means the user's local application data folder,
    /// which both hosts resolve alike; a relative path would not, because each host resolves it against its own working directory.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
}

/// <summary>
/// Stores files as <c>root/ab/abcdef…</c> under a random hex key. Keys come only from this class, and
/// <see cref="PathOf"/> refuses anything else, so a key can never point outside the root.
/// </summary>
public sealed class LocalFileStore(IOptions<FileStorageOptions> options, IHostEnvironment environment) : IFileStore
{
    private const string KeyFormat = "N";
    private const int ShardLength = 2;
    private const int KeyLength = 32;
    private const string DefaultFolder = "tgysm";
    private const string FilesFolder = "files";

    private static readonly string MissingRootMessage =
        $"{FileStorageOptions.SectionName}:{nameof(FileStorageOptions.RootPath)} must be set outside Development: the API and the Worker " +
        "must share one absolute file-store folder.";

    /// <summary>The absolute folder files are stored under.</summary>
    public string Root { get; } = ResolveRoot(options.Value, environment);

    private static string ResolveRoot(FileStorageOptions options, IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(options.RootPath))
        {
            return Path.GetFullPath(options.RootPath);
        }

        return environment.IsDevelopment()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DefaultFolder, FilesFolder)
            : throw new InvalidOperationException(MissingRootMessage);
    }

    public async Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString(KeyFormat);
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(content, cancellationToken);
        return key;
    }

    public async Task<string> SaveAsync(Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString(KeyFormat);
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await write(stream, cancellationToken);
        }
        catch
        {
            // Includes cancellation: a half-written file must never stay behind without a key anyone knows.
            File.Delete(path);
            throw;
        }

        return key;
    }

    public Stream? OpenRead(string storageKey)
    {
        var path = PathOf(storageKey);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    public void Delete(string storageKey)
    {
        var path = PathOf(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string PathOf(string key)
    {
        if (key.Length != KeyLength || !key.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        return Path.Combine(Root, key[..ShardLength], key);
    }
}

/// <summary>
/// Logs the resolved file-store root once at host startup (API and Worker), and so fails the host at startup when the root is
/// missing outside Development instead of at the first upload or export.
/// </summary>
public sealed class FileStoreRootLog(LocalFileStore store, ILogger<FileStoreRootLog> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("File store root: {FileStoreRoot}", store.Root);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Development scanner: files that passed the type check are clean. Replace with a real scanner in production.</summary>
public sealed class DevelopmentFileScanner : IFileScanner
{
    public Task<bool> IsCleanAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(true);
}
