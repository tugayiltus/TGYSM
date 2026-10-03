namespace Tgysm.Application.Abstractions;

/// <summary>File storage outside the web root with random keys (ADR-0012): attachments and export files.</summary>
public interface IFileStore
{
    /// <summary>Stores the bytes and returns a new random storage key.</summary>
    Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new file, lets <paramref name="write"/> stream into it and returns its new random storage key. When
    /// <paramref name="write"/> throws, the partly written file is deleted before the exception propagates, so no orphan is left.
    /// </summary>
    Task<string> SaveAsync(Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken);

    /// <summary>Opens a stored file for reading, or null when it is missing.</summary>
    Stream? OpenRead(string storageKey);

    void Delete(string storageKey);
}

/// <summary>Malware scan port (ADR-0012). Only files it reports clean can be downloaded.</summary>
public interface IFileScanner
{
    Task<bool> IsCleanAsync(string storageKey, CancellationToken cancellationToken);
}
