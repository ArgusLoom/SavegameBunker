using System.IO;
using System.Reflection;
using System.Text.Json;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// The bundled known-games table: a hand-curated list of popular titles mapping a
/// display name and its aliases to a <c>saveDirTemplate</c>, so the Add Game dialog can fill the
/// save folder for a user who does not know where their game keeps its saves. The table ships as
/// an embedded resource — inside the executable, not a second file beside it — and a
/// missing or unreadable resource degrades the dialog to plain browsing rather than surfacing an
/// error the user cannot act on.
/// </summary>
public static class KnownGamesCatalog
{
    private const string ResourceName = "SavegameBunker.Data.known-games.json";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly IReadOnlyList<KnownGame> Entries = Load();

    /// <summary>Every entry in the table, in file order.</summary>
    public static IReadOnlyList<KnownGame> All => Entries;

    /// <summary>
    /// Case-insensitive substring match of <paramref name="query"/> against every entry's display
    /// name and aliases, ordered by how early the match starts (earliest first) and then
    /// alphabetically by display name, capped at <paramref name="max"/> results. A blank query
    /// returns no suggestions — the popup has nothing to show until the user types something.
    /// </summary>
    public static IReadOnlyList<KnownGame> Find(string query, int max)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
            return [];

        return Entries
            .Select(game => (Game: game, Index: EarliestMatch(game, trimmed)))
            .Where(x => x.Index >= 0)
            .OrderBy(x => x.Index)
            .ThenBy(x => x.Game.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(x => x.Game)
            .ToList();
    }

    private static int EarliestMatch(KnownGame game, string query)
    {
        var best = game.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        foreach (var alias in game.Aliases)
        {
            var index = alias.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (best < 0 || index < best))
                best = index;
        }

        return best;
    }

    /// <summary>
    /// True when <paramref name="query"/> equals one table entry's display name or an alias
    /// (OrdinalIgnoreCase). Ambiguous ties (two entries sharing the same alias) return false so
    /// the dialog does not guess. Substring hits are not exact — those stay in
    /// <see cref="Find"/>'s popup.
    /// </summary>
    public static bool TryExactMatch(string query, out KnownGame? game)
    {
        game = null;
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
            return false;

        KnownGame? found = null;
        foreach (var entry in Entries)
        {
            if (!IsExactNameOrAlias(entry, trimmed))
                continue;
            if (found is not null)
            {
                game = null;
                return false;
            }

            found = entry;
        }

        game = found;
        return found is not null;
    }

    private static bool IsExactNameOrAlias(KnownGame game, string query)
    {
        if (string.Equals(game.DisplayName, query, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var alias in game.Aliases)
        {
            if (string.Equals(alias, query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IReadOnlyList<KnownGame> Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            return LoadFrom(stream);
        }
        catch
        {
            // A corrupt embedded resource must not crash the dialog — degrade to plain browsing.
            return [];
        }
    }

    /// <summary>
    /// Test seam: loads from an arbitrary stream rather than the assembly's own manifest resource,
    /// so the degrade-to-empty path can be exercised with a missing/null stream without reflecting
    /// over the real assembly.
    /// </summary>
    internal static IReadOnlyList<KnownGame> LoadFrom(Stream? stream)
    {
        if (stream is null)
            return [];

        try
        {
            return (IReadOnlyList<KnownGame>?)JsonSerializer.Deserialize<List<KnownGame>>(stream, ReadOptions)
                ?? [];
        }
        catch
        {
            return [];
        }
    }
}
