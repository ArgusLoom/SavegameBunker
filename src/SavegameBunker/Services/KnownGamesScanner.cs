using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Probes the bundled known-games table against this machine and reports which entries
/// have save data already on disk. Stateless, no WPF reference.
/// </summary>
/// <remarks>
/// <see cref="Scan"/> is deliberately synchronous, unlike <see cref="SavegameBunker.ViewModels.MainViewModel"/>'s
/// bounded-parallel status sweep: every step it calls — <see cref="PathResolver.TryResolve"/> and
/// <see cref="SaveStatusScanner.Scan"/> — is itself synchronous, so wrapping this method in
/// <c>async</c> would be a signature that promises asynchrony over work that has none, the same
/// reasoning CLAUDE.md records for wrapping <c>ZipFile.CreateFromDirectory</c> in
/// <see cref="Task.Run{TResult}(Func{TResult})"/> instead of faking an <c>async</c> method.
/// <see cref="SavegameBunker.ViewModels.MainViewModel"/> supplies the actual off-UI-thread wrapper by
/// calling this from inside its own <c>Task.Run</c>.
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
    /// blocked on a sleeping network drive must not serialize the whole probe, but nothing caps the
    /// window from opening the moment the sweep in fact returns.
    /// </summary>
    private const int MaxDegreeOfParallelism = 4;

    /// <summary>
    /// Every <c>SaveDirTemplate</c> held by more than one entry in <paramref name="table"/>:
    /// a handful of the shipped table's entries sit on the same Ubisoft launcher
    /// folder, and reporting that folder once per entry would, if confirmed, write one catalog
    /// entry per entry backing up the same folder into identical archives. The rule is derived by
    /// counting the loaded table at runtime — grouping by the raw template string, not a resolved
    /// absolute path, because resolution is per-machine I/O-free work duplicated across every
    /// entry for no gain, and because the design's own measured fact (a handful of distinct
    /// <c>saveDirTemplate</c> values sharing one folder) was itself computed from the raw template
    /// string. A second collision group added to the table later is caught by this same counting
    /// rule with no code change.
    /// <c>public</c> and <c>static</c> because <c>AddGameDialogViewModel</c> consumes
    /// the identical rule — a second copy of it in that view model would be free to drift from
    /// this one.
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
        // resolution: the design says a shared entry is excluded "before any of that", so it is
        // reported as shared even when its id is unsafe, its template does not resolve, or its
        // directory is missing. Do not reorder this below the IdSlug.IsSafe gate — that would
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
            // skip, per the locked ScanResult shape, which has no bucket for it. The other entries
            // in the table must still be probed: one bad template must not abort the whole scan.
            if (!PathResolver.TryResolve(entry.SaveDirTemplate, null, out var path, out _))
                continue;

            // Already-known check sits here, before the filesystem probe: it costs no I/O, and
            // skipping the walk for an entry the catalog already covers is the whole point of
            // doing it before the probe on a table of 268 templates. Trimmed the same
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
