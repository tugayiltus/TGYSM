namespace Tgysm.Domain.Common;

/// <summary>
/// Version 7 ids that keep creation order inside one process. <see cref="Guid.CreateVersion7()"/> is random
/// within a millisecond, so rows written in the same instant (and ordered by time, then id) can swap places.
/// </summary>
public static class SequentialId
{
    private const int GuidLength = 16;
    // Bytes 9–15 (big-endian) are random bits only; byte 8 carries the variant, bytes 0–7 time and version.
    private const int FirstCounterByte = 9;

    private static readonly Lock Gate = new();
    private static readonly byte[] Last = new byte[GuidLength];

    public static Guid Next()
    {
        Span<byte> candidate = stackalloc byte[GuidLength];
        Guid.CreateVersion7().TryWriteBytes(candidate, bigEndian: true, out _);

        lock (Gate)
        {
            if (candidate.SequenceCompareTo(Last) <= 0)
            {
                Last.CopyTo(candidate);
                Increment(candidate);
            }

            candidate.CopyTo(Last);
        }

        return new Guid(candidate, bigEndian: true);
    }

    private static void Increment(Span<byte> bytes)
    {
        for (var i = GuidLength - 1; i >= FirstCounterByte; i--)
        {
            if (++bytes[i] != 0)
            {
                return;
            }
        }
    }
}
