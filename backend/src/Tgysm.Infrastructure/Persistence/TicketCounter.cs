namespace Tgysm.Infrastructure.Persistence;

/// <summary>Last issued ticket number per kind and year (ADR-0011); written only by <see cref="TicketNumberGenerator"/>.</summary>
public sealed class TicketCounter
{
    public string Kind { get; set; } = string.Empty;
    public int Year { get; set; }
    public long LastValue { get; set; }
}
