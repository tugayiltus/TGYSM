namespace Tgysm.Domain.Common;

/// <summary>
/// PostgreSQL <c>timestamptz</c> keeps microseconds, while .NET clocks can tick every 100 ns (Linux). A value returned
/// to a caller before the save must compare equal to the same value read back later.
/// </summary>
public static class StoredTime
{
    public static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
}
