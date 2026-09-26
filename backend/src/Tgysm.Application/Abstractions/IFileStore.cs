namespace Tgysm.Application.Abstractions;

/// <summary>Attachment storage outside the web root with random keys (ADR-0012).</summary>
public interface IFileStore
{
    /// <summary>Stores the bytes and returns a new random storage key.</summary>
    Task<string> SaveAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Opens a stored file for reading, or null when it is missing.</summary>
    Stream? OpenRead(string storageKey);

    void Delete(string storageKey);
}

/// <summary>Malware scan port (ADR-0012). Only files it reports clean can be downloaded.</summary>
public interface IFileScanner
{
    Task<bool> IsCleanAsync(string storageKey, CancellationToken cancellationToken);
}
