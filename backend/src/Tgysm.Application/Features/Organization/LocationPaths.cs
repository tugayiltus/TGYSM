using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Organization;

/// <summary>Full location paths ("Kampüs / Bina") built from the location tree, for CSV matching and the template.</summary>
internal sealed class LocationPaths
{
    public const string Separator = " / ";
    /// <summary>The CSV importer splits a typed location path on this; a location name holding it cannot be addressed.</summary>
    public const char SegmentSeparator = '/';

    private readonly List<Entry> _entries;

    private LocationPaths(List<Entry> entries) => _entries = entries;

    public sealed record Entry(Guid Id, string Path, IReadOnlyList<string> Keys, bool IsActive);

    public sealed record Match(Entry? Location, string? ErrorCode);

    public IReadOnlyList<Entry> Entries => _entries;

    public static async Task<LocationPaths> LoadAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var nodes = await db.Locations.AsNoTracking()
            .Select(l => new { l.Id, l.Name, l.NormalizedName, l.ParentId, l.IsActive })
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var entries = new List<Entry>(nodes.Count);
        foreach (var node in nodes.Values)
        {
            var names = new List<string>();
            var keys = new List<string>();
            var visited = new HashSet<Guid>();
            for (var current = node; current is not null && visited.Add(current.Id);
                 current = current.ParentId is { } parent ? nodes.GetValueOrDefault(parent) : null)
            {
                names.Insert(0, current.Name);
                keys.Insert(0, current.NormalizedName);
            }

            entries.Add(new Entry(node.Id, string.Join(Separator, names), keys, node.IsActive));
        }

        return new LocationPaths(entries);
    }

    /// <summary>
    /// Matches a path by its trailing segments, so "A Blok" works while it is unique and "Kampüs / A Blok" when it is
    /// not. Ignores case and Turkish letter case.
    /// </summary>
    public Match Find(string path)
    {
        var wanted = path.Split(SegmentSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(TurkishText.ToSearchKey)
            .ToList();
        var candidates = _entries.Where(e => EndsWith(e.Keys, wanted)).Take(2).ToList();
        return candidates switch
        {
            [] => new Match(null, ErrorCodes.ImportLocationNotFound),
            [var only] when !only.IsActive => new Match(null, ErrorCodes.ReferenceInactive),
            [var only] => new Match(only, null),
            _ => new Match(null, ErrorCodes.ImportLocationAmbiguous),
        };
    }

    private static bool EndsWith(IReadOnlyList<string> keys, List<string> wanted) =>
        wanted.Count > 0 && wanted.Count <= keys.Count && keys.Skip(keys.Count - wanted.Count).SequenceEqual(wanted);
}
