using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;

namespace Tgysm.Infrastructure.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "Files";

    /// <summary>Folder for attachments; empty means the user's local application data folder.</summary>
    public string RootPath { get; set; } = string.Empty;
}

/// <summary>
/// Stores files as <c>root/ab/abcdef…</c> under a random hex key. Keys come only from this class, and
/// <see cref="PathOf"/> refuses anything else, so a key can never point outside the root.
/// </summary>
public sealed class LocalFileStore(IOptions<FileStorageOptions> options) : IFileStore
{
    private const string KeyFormat = "N";
    private const int ShardLength = 2;
    private const int KeyLength = 32;
    private const string DefaultFolder = "tgysm";
    private const string FilesFolder = "files";

    private readonly string _root = string.IsNullOrWhiteSpace(options.Value.RootPath)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DefaultFolder, FilesFolder)
        : Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString(KeyFormat);
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(content, cancellationToken);
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

        return Path.Combine(_root, key[..ShardLength], key);
    }
}

/// <summary>Development scanner: files that passed the type check are clean. Replace with a real scanner in production.</summary>
public sealed class DevelopmentFileScanner : IFileScanner
{
    public Task<bool> IsCleanAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(true);
}
