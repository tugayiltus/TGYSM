namespace Tgysm.Domain.Tickets;

/// <summary>Impact × urgency → priority (spec §2.2).</summary>
public static class PriorityMatrix
{
    private static readonly Priority[,] Matrix =
    {
        // urgency:  high         medium       low
        { Priority.P1, Priority.P2, Priority.P3 }, // impact high
        { Priority.P2, Priority.P3, Priority.P4 }, // impact medium
        { Priority.P3, Priority.P4, Priority.P4 }, // impact low
    };

    public static Priority Calculate(Level impact, Level urgency) => Matrix[(int)impact, (int)urgency];
}
