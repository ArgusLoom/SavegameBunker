using System.Collections.Concurrent;
using System.IO;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Probes the bundled known-games table against this machine and reports which entries
/// have save data already on disk. Stateless, no WPF reference.
/// </summary>
/// <remarks>
/// <see cref="Scan"/> is deliberately synchronous: every step it calls —
/// <see cref="PathResolver.TryResolve"/> and <see cref="SaveStatusScanner.Scan"/> — is itself
/// synchronous, and an <c>async</c> signature over work with no asynchrony in it would be a fake.
/// <see cref="SavegameBunker.ViewModels.MainViewModel"/> keeps it off the UI thread by calling it
/// inside <see cref="Task.Run{TResult}(Func{TResult})"/>.
///
/// <see cref="Scan"/> never throws across its boundary: <see cref="SaveStatusScanner.Scan"/> has
/// already converted every filesystem failure it can encounter into a <see cref="SaveStatus"/>
/// before this method ever sees it, so there is no outer try/catch here swallowing information —
/// there is nothing left that can throw.
/// </remarks>
public sealed class KnownGamesScanner
{
    /// <summary>
    /// Same bound the status sweep (<c>StatusSweepCoordinator</c>) uses: a handful of templates
    /// blocked on a sleeping network drive must not serialize the whole probe, because the Find
    /// Games window opens only once the probe returns.
    /// </summary>
    private const int MaxDegreeOfParallelism = 4;

    /// <summary>
    /// Every <c>SaveDirTemplate</c> held by more than one entry in <paramref name="table"/>:
    /// several of the shipped table's entries sit on the same launcher folder (Ubisoft's, for one),
    /// and offering that folder once per entry would create catalog entries that back up the same
    /// folder into identical archives. Grouped by the raw template string, so the answer is the
    /// same on every machine and needs no path resolution; any new collision group in the table is
    /// caught without a code change. <c>public</c> and <c>static</c> because
    /// <c>AddGameDialogViewModel</c> applies the identical rule and must not keep a drifting copy.
    /// </summary>
    public static IReadOnlySet<string> SharedTemplates(IReadOnlyList<KnownGame> table)
    {
        return table
            .GroupBy(entry => entry.SaveDirTemplate, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <param name="table">The known-games table to probe, in any order.</param>
    /// <param name="catalog">
    /// The catalog's current entries, used to exclude a table entry already covered by id or by
    /// resolved path.
    /// </param>
    public ScanResult Scan(IReadOnlyList<KnownGame> table, IReadOnlyList<Game> catalog)
    {
        // The shared-template exclusion runs first, ahead of the id-safety gate and any
        // resolution, so a shared entry is reported as shared even when its id is unsafe, its
        // template does not resolve, or its directory is missing. Do not reorder this below the IdSlug.IsSafe gate — that would
        // silently change which reason a shared-and-unsafe entry is reported under.
        var sharedTemplates = SharedTemplates(table);
        var skippedShared = table
            .Where(entry => sharedTemplates.Contains(entry.SaveDirTemplate))
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
        var eligible = table.Where(entry => !sharedTemplates.Contains(entry.SaveDirTemplate)).ToList();

        // The only runtime gate between a malformed known-games table id and a folder name under
        // backupRoot: KnownGamesCatalog.Load performs no per-entry validation, and
        // GameCatalog.SaveAsync validates nothing it is handed either. An unsafe id is skipped
        // silently here — it contributes to no hit and no reported skip, because it should never
        // have shipped in the table in the first place.
        var safeEntries = eligible.Where(entry => IdSlug.IsSafe(entry.Id)).ToList();

        // Built once from the catalog argument, ahead of resolving the table:
        // an id match needs no filesystem access at all, and a path match reuses PathResolver's own
        // normalization instead of comparing raw template text, so %APPDATA%/X, %APPDATA%\X and a
        // path reached through Browse all collapse to one comparable string. A catalog entry whose
        // own template does not resolve contributes nothing to the path set and is not an error —
        // it simply cannot suppress anything by path (it can still suppress by id).
        var knownIds = new HashSet<string>(catalog.Select(game => game.Id), StringComparer.OrdinalIgnoreCase);
        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in catalog)
        {
            if (PathResolver.TryResolve(game.SaveDirTemplate, null, out var catalogPath, out _))
                knownPaths.Add(catalogPath.TrimEnd(Path.DirectorySeparatorChar));
        }

        var resolved = new List<(KnownGame Entry, string Path)>();

        // A plain local counter, not an interlocked one: this loop runs sequentially, well before
        // the bounded-parallel probe below, so there is no concurrent writer to guard against.
        var skippedAlreadyKnown = 0;
        foreach (var entry in safeEntries)
        {
            // A template that does not resolve is dropped silently — neither a hit nor a reported
            // skip, because ScanResult has no bucket for it. The other entries
            // in the table must still be probed: one bad template must not abort the whole scan.
            if (!PathResolver.TryResolve(entry.SaveDirTemplate, null, out var path, out _))
                continue;

            // Already-known check sits here, before the filesystem probe: it costs no I/O, and
            // skipping the walk for an entry the catalog already covers is the whole point of
            // doing it before the probe across the whole table. Trimmed the same
            // way knownPaths was built, so a trailing separator on either side never breaks the
            // match.
            if (knownIds.Contains(entry.Id) || knownPaths.Contains(path.TrimEnd(Path.DirectorySeparatorChar)))
            {
                skippedAlreadyKnown++;
                continue;
            }

            resolved.Add((entry, path));
        }

        var hits = new ConcurrentBag<ScanHit>();
        Parallel.ForEach(
            resolved,
            new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism },
            item =>
            {
                var probe = new Game
                {
                    Id = item.Entry.Id,
                    DisplayName = item.Entry.DisplayName,
                    SaveDirTemplate = item.Entry.SaveDirTemplate,
                };

                // Reuses SaveStatusScanner's walk verbatim rather than re-implementing it: it
                // already tolerates locked subfolders and reparse-point loops and has its own
                // pinning tests. Only Found — an existing directory holding at least one file — is
                // a hit; NoSaves (missing or existing-but-empty) and PathError are both "not a hit".
                var status = SaveStatusScanner.Scan(probe);
                if (status.Kind == SaveStatusKind.Found)
                    hits.Add(new ScanHit(item.Entry, item.Path, (int)status.FileCount, status.TotalBytes));
            });

        // ConcurrentBag yields in completion order, which varies run to run under bounded
        // parallelism — the sort here is load-bearing, not cosmetic, or the window's row order
        // would be nondeterministic.
        var sortedHits = hits
            .OrderBy(hit => hit.Game.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(hit => hit.Game.Id, StringComparer.Ordinal)
            .ToList();

        return new ScanResult(sortedHits, skippedShared, skippedAlreadyKnown);
    }
}
