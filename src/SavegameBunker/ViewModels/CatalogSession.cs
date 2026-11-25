using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.ViewModels;

/// <summary>
/// Owns the loaded catalog record and every write to the games file — the structural form
/// of the "no second writer" rule: this is the only file in the tree that calls
/// <see cref="GameCatalog.SaveAsync(string, Catalog, DateTime?)"/>.
/// </summary>
/// <remarks>
/// <c>internal</c> and held only as a private readonly field on <see cref="MainViewModel"/> —
/// never as a public property — so nothing in XAML can bind a path to the catalog writer and
/// nothing outside the assembly can reach it. It references no WPF type, raises no
/// change notification and composes no user-facing sentence: notification and wording stay with
/// the view model, so every header binding and status line has one owner.
/// </remarks>
internal sealed class CatalogSession
{
    // Mutable: Refresh and every successful write reassign this record (a `with` copy) rather
    // than mutating its own Catalog reference.
    private CatalogLoad _load;

    /// <summary>
    /// Serializes every mutate-save-rollback sequence and the reload. Each one changes the held
    /// record, awaits the file write, then commits or rolls back; two interleaving would save with
    /// the same last-seen stamp (one is then refused as "changed on disk") or roll back the other's
    /// change. Command-level gates in the view model keep rows consistent; this guarantees the
    /// file writes themselves never overlap, whichever commands happen to race.
    /// </summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private async Task<T> ExclusiveAsync<T>(Func<Task<T>> body)
    {
        await _writeLock.WaitAsync();
        try
        {
            return await body();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public CatalogSession(CatalogLoad load)
    {
        _load = load;
    }

    public string Path => _load.Path;
    public IReadOnlyList<Game> Games => _load.Catalog.Games;
    public IReadOnlyList<string> Problems => _load.Problems;
    public bool Seeded => _load.Seeded;
    public string? FatalError => _load.FatalError;

    /// <summary>
    /// The bare resolve — what the header line (<see cref="MainViewModel.BackupRootDisplay"/>)
    /// uses. Distinct from <see cref="TryGetConfiguredBackupRoot"/>: this can succeed on a
    /// catalog that has no folder configured, resolving to wherever an unset value falls back to.
    /// </summary>
    public bool TryResolveBackupRoot(out string root) =>
        GameCatalog.TryResolveBackupRoot(_load, out root, out _);

    /// <summary>
    /// The bare resolve <b>and</b> the non-blank check on the stored folder — the predicate the
    /// status sweep, the restore pre-flight and the ensure-folder helper each apply. Not
    /// interchangeable with <see cref="TryResolveBackupRoot"/>: folding the two together would
    /// either change what the header shows or change when the first-backup picker opens.
    /// </summary>
    public bool TryGetConfiguredBackupRoot(out string root) =>
        GameCatalog.TryResolveBackupRoot(_load, out root, out _)
            && !string.IsNullOrWhiteSpace(_load.Catalog.BackupRoot);

    /// <summary>
    /// The one call site that writes this session's catalog, wrapping
    /// <see cref="GameCatalog.SaveAsync(string, Catalog, DateTime?)"/> with the stamp this session
    /// last saw on disk (<see cref="GameCatalog.SaveAsync(string, Catalog, DateTime?)"/>'s own doc
    /// comment explains why) and refreshing that stamp on success — otherwise this session's own
    /// very next write would immediately fail its own check against the file it just wrote.
    /// </summary>
    private async Task<OperationResult> SaveAsync()
    {
        var result = await GameCatalog.SaveAsync(_load.Path, _load.Catalog, _load.LastWriteTimeUtc);
        if (result.Success)
        {
            // The stamp read can throw (the just-written file vanished again, a transient share
            // error) — the save itself already succeeded, so the failure must not escape this
            // async method into a caller's async-void command path. Keeping the previous stamp
            // makes the NEXT save fail its own changed-on-disk check — a named refusal, not a
            // crash.
            try
            {
                _load = _load with { LastWriteTimeUtc = File.GetLastWriteTimeUtc(_load.Path) };
            }
            catch (Exception)
            {
                // Keep the previous stamp.
            }
        }

        return result;
    }

    /// <summary>
    /// Appends <paramref name="games"/> and saves, removing exactly what was appended on failure.
    /// Serves both add-game and find-games, so the append-and-rollback exists once; both callers
    /// report the returned result themselves.
    /// </summary>
    public Task<OperationResult> AddGamesAsync(IReadOnlyList<Game> games) => ExclusiveAsync(() => AddGamesCoreAsync(games));

    private async Task<OperationResult> AddGamesCoreAsync(IReadOnlyList<Game> games)
    {
        if (FatalError is not null)
            return OperationResult.Fail(Strings.Get("str_write_blockedByFatalError"));

        // Re-checked here, under the write lock: a caller's id snapshot can be stale (Add Game
        // takes it before its dialog, and Find Games may add the same id meanwhile). Two entries
        // with one id would share one archive folder.
        var taken = games
            .Where(g => _load.Catalog.Games.Any(existing => string.Equals(existing.Id, g.Id, StringComparison.OrdinalIgnoreCase)))
            .Select(g => g.DisplayName)
            .ToList();
        if (taken.Count > 0)
            return OperationResult.Fail(Strings.Format("str_write_idTaken", string.Join(", ", taken)));

        _load.Catalog.Games.AddRange(games);
        var saveResult = await SaveAsync();
        if (!saveResult.Success)
        {
            foreach (var game in games)
                _load.Catalog.Games.Remove(game);
        }

        return saveResult;
    }

    /// <summary>
    /// Rebuilds the record with <paramref name="picked"/> as the backup folder, in the catalog's
    /// own separator form, and saves — restoring the previous record on failure. The caller
    /// reports the failure and raises the header's change notification on success.
    /// </summary>
    public Task<OperationResult> SetBackupRootAsync(string picked) => ExclusiveAsync(() => SetBackupRootCoreAsync(picked));

    private async Task<OperationResult> SetBackupRootCoreAsync(string picked)
    {
        if (FatalError is not null)
            return OperationResult.Fail(Strings.Get("str_write_blockedByFatalError"));

        var previousLoad = _load;
        _load = _load with
        {
            Catalog = new Catalog
            {
                BackupRoot = picked.Replace('\\', '/'),
                Games = _load.Catalog.Games,
            },
        };

        var saveResult = await SaveAsync();
        if (!saveResult.Success)
            _load = previousLoad;

        return saveResult;
    }

    /// <summary>
    /// Re-reads the catalog file from the same path and replaces the held record. The caller
    /// inspects <see cref="FatalError"/> afterwards and leaves the previously loaded rows on
    /// screen when the file is malformed. A fatal reload leaves the previously held record
    /// in place instead — replacing it with the empty catalog <see cref="GameCatalog.LoadAsync"/>
    /// returns on failure would make every later write (add game, change backup folder) save that
    /// empty catalog over the real one still on disk.
    /// </summary>
    public Task ReloadAsync() => ExclusiveAsync(async () =>
    {
        var reloaded = await GameCatalog.LoadAsync(_load.Path);
        if (reloaded.FatalError is not null)
        {
            _load = _load with { FatalError = reloaded.FatalError };
            return true;
        }

        _load = reloaded;
        return true;
    });

    /// <summary>
    /// Replaces one game's stored save-folder template and saves — restoring the previous entry at
    /// the same index on failure. The replacement carries <paramref name="game"/>'s
    /// own id and display name by construction (<see cref="Game"/> is init-only, so this constructs
    /// a new instance rather than mutating), so the repair route can never rename the folder
    /// holding that game's backups. Looked up by reference rather than by index: the caller (the
    /// repair route) only has the row's <see cref="Game"/>, not its position in this list.
    /// </summary>
    public Task<OperationResult> SetSaveFolderAsync(Game game, string newSaveDirTemplate) =>
        ExclusiveAsync(() => SetSaveFolderCoreAsync(game, newSaveDirTemplate));

    private async Task<OperationResult> SetSaveFolderCoreAsync(Game game, string newSaveDirTemplate)
    {
        if (FatalError is not null)
            return OperationResult.Fail(Strings.Get("str_write_blockedByFatalError"));

        var index = _load.Catalog.Games.IndexOf(game);
        if (index < 0)
            return OperationResult.Fail(Strings.Get("str_changeSaveFolder_notFound"));

        var previous = _load.Catalog.Games[index];
        _load.Catalog.Games[index] = new Game
        {
            Id = previous.Id,
            DisplayName = previous.DisplayName,
            SaveDirTemplate = newSaveDirTemplate,
        };

        var saveResult = await SaveAsync();
        if (!saveResult.Success)
            _load.Catalog.Games[index] = previous;

        return saveResult;
    }

    /// <summary>
    /// Drops <paramref name="game"/> from the catalog and saves — putting it back at the same
    /// index on failure. Does not touch the save folder or any backup archives: those are the
    /// user's files, and removing a list entry is not a delete. Looked up by reference, matching
    /// <see cref="SetSaveFolderAsync"/>, because the caller only has the row's <see cref="Game"/>.
    /// </summary>
    public Task<OperationResult> RemoveGameAsync(Game game) => ExclusiveAsync(() => RemoveGameCoreAsync(game));

    private async Task<OperationResult> RemoveGameCoreAsync(Game game)
    {
        if (FatalError is not null)
            return OperationResult.Fail(Strings.Get("str_write_blockedByFatalError"));

        var index = _load.Catalog.Games.IndexOf(game);
        if (index < 0)
            return OperationResult.Fail(Strings.Get("str_removeGame_notFound"));

        var previous = _load.Catalog.Games[index];
        _load.Catalog.Games.RemoveAt(index);

        var saveResult = await SaveAsync();
        if (!saveResult.Success)
            _load.Catalog.Games.Insert(index, previous);

        return saveResult;
    }

    /// <summary>
    /// Splits <paramref name="picks"/> into the entries that may be added and the display names
    /// of the ones refused for colliding with an existing or an intra-batch id — the id
    /// names the folder holding the user's backups, so a collision is refused by name rather than
    /// renamed, suffixed or overwritten. Existing ids are read fresh from the catalog each time
    /// this runs rather than cached before the probe, so a catalog changed mid-scan (Refresh, Add
    /// game) is still respected. Composes no sentence — the caller writes its own refusal
    /// messages from the two returned lists.
    /// </summary>
    public (List<Game> Accepted, List<string> Refused) PartitionNewGames(IReadOnlyList<KnownGame> picks)
    {
        // HashSet.Add doubles as the intra-batch guard: two picks sharing an id refuse the
        // second one here too, with no second code path.
        var existingIds = new HashSet<string>(_load.Catalog.Games.Select(g => g.Id), StringComparer.OrdinalIgnoreCase);

        var accepted = new List<Game>();
        var refused = new List<string>();
        foreach (var pick in picks)
        {
            if (existingIds.Add(pick.Id))
                accepted.Add(new Game { Id = pick.Id, DisplayName = pick.DisplayName, SaveDirTemplate = pick.SaveDirTemplate });
            else
                refused.Add(pick.DisplayName);
        }

        return (accepted, refused);
    }
}
