using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.ViewModels;

/// <summary>
/// The status-sweep engine behind <see cref="MainViewModel"/>'s rescans: resetting rows to
/// Checking, walking each row's save folder and comparing it against the backup folder, and the
/// free-space advisory's own filesystem reads.
/// </summary>
/// <remarks>
/// <c>internal</c> and held only as a private readonly field on <see cref="MainViewModel"/> —
/// never as a public property — so nothing in XAML can bind a path to it. It references no WPF
/// type and holds no observable state: the one mutable field it does hold (the backup-root
/// reachability memo) is bookkeeping for <see cref="WalkAsync"/>'s own narrowed-scan widening,
/// never anything bound. <see cref="MainViewModel"/> keeps every bound property and constructs
/// every <c>Progress&lt;T&gt;</c> itself — this class takes none and builds none.
/// <para>
/// Every write <see cref="WalkAsync"/> applies to a row goes through the <c>onUi</c> delegate
/// passed to the constructor, with the per-row token check made INSIDE that callback:
/// <c>onUi</c> only schedules the callback, it does not run it, so a check made on the
/// calling thread and a newer scan starting on the UI thread before the callback actually
/// executes would race. <see cref="MainViewModel.IsAnyRowChecking"/> stays MainViewModel's
/// aggregate — <see cref="Reset"/> is what flips it true for the rows it touches; this class
/// never reads or writes it directly.
/// </para>
/// </remarks>
internal sealed class StatusSweepCoordinator
{
    private readonly BackupService _backup;
    private readonly Action<Action> _onUi;

    /// <summary>
    /// The backup-root reachability the most recent <see cref="WalkAsync"/> call observed — null
    /// when no root is configured. Lets a narrowed scan widen to a full sweep exactly once, on
    /// the actual false-to-true flip, instead of re-checking every row's current (possibly
    /// stale) <see cref="GameRowViewModel.ComparisonKind"/> on every call.
    /// </summary>
    private bool? _lastBackupRootReachable;

    public StatusSweepCoordinator(BackupService backup, Action<Action> onUi)
    {
        _backup = backup;
        _onUi = onUi;
    }

    /// <summary>
    /// Bumps every row's own scan token and resets both axes to Checking — the token is per row,
    /// not per sweep, because scans cover different sets of rows: a newer scan of one row
    /// must not invalidate an older sweep's still-pending results for all the others. Materialises
    /// the snapshot (the token bump) before applying any reset, so every row's token is captured
    /// at the same moment.
    /// </summary>
    public IReadOnlyList<(GameRowViewModel Row, int Token)> Reset(IEnumerable<GameRowViewModel> rows)
    {
        var snapshot = rows.Select(row => (Row: row, Token: ++row.ScanToken)).ToArray();
        foreach (var (row, _) in snapshot)
        {
            row.ApplyStatus(SaveStatus.Checking());
            row.ApplyBackupComparison(BackupComparison.Checking());
        }

        return snapshot;
    }

    /// <summary>
    /// The worker half of a sweep: reads backup-root reachability once, walks every row in
    /// <paramref name="snapshot"/> at bounded concurrency, and applies each result through the
    /// constructor's <c>onUi</c> delegate. Blocks on the reachability probe before its first
    /// await, so it must never be called on the UI thread. Returns whether the backup root just
    /// became reachable on the actual false-to-true flip, so the caller can decide whether a
    /// narrowed scan needs one follow-up full sweep.
    /// </summary>
    public async Task<bool> WalkAsync(
        IReadOnlyList<(GameRowViewModel Row, int Token)> snapshot, bool hasRoot, string root)
    {
        var reachable = hasRoot && _backup.IsBackupRootReachable(root);

        // Comparing against the LAST reachability this coordinator itself observed — not every
        // row's current, possibly stale, ComparisonKind — means a narrowed scan's widening only
        // ever fires once, on the actual flip, instead of forcing every narrowed scan back to a
        // full sweep for as long as any row happens to still show that verdict.
        var becameReachable = hasRoot && reachable && _lastBackupRootReachable == false;
        _lastBackupRootReachable = hasRoot ? reachable : null;

        await Parallel.ForEachAsync(snapshot, new ParallelOptions { MaxDegreeOfParallelism = 4 },
            async (entry, _) =>
            {
                var (row, token) = entry;
                // Scanning stays off the UI thread; APPLYING the result does not. These setters
                // raise PropertyChanged, and what hangs off those bindings has WPF thread
                // affinity — applying them from a worker threw "The calling thread cannot
                // access this object because a different thread owns it" on a plain Refresh.
                var status = await Task.Run(() => SaveStatusScanner.Scan(row.Game));
                // A newer scan of this row (a backup/restore's own post-outcome rescan, say) may
                // already have moved it past "Checking" by the time this one's result comes
                // back — applying it here would stomp the newer, more current verdict with a
                // stale one. The token check runs INSIDE the onUi callback, not before onUi is
                // called: onUi only schedules the callback, it does not run it, so a check made
                // on this worker thread and a newer scan starting on the UI thread before that
                // callback actually executes would race — checking where the write itself
                // happens is the only way to close that window.
                _onUi(() =>
                {
                    if (token == row.ScanToken)
                        row.ApplyStatus(status);
                });

                var rowReachable = false;
                var backups = (IReadOnlyList<BackupArchive>)[];
                if (reachable)
                {
                    (rowReachable, backups) = await Task.Run(() =>
                    {
                        var listable = _backup.TryListBackups(root, row.Game.Id, out var found);
                        return (listable, found);
                    });
                }

                var comparison = BackupComparer.Compare(status, rowReachable, backups, row.RestoredArchiveTakenAt);
                _onUi(() =>
                {
                    if (token == row.ScanToken)
                        row.ApplyBackupComparison(comparison);
                });
            });

        return becameReachable;
    }

    /// <summary>
    /// The free-space advisory's own filesystem reads: one free-space probe plus one
    /// <see cref="BackupService.ComputeSaveDataSize"/> call per relevant game, all inside one
    /// <c>Task.Run</c> — checking a box must never block on a directory walk.
    /// </summary>
    public Task<(string? Free, string? Selected, string? Changed)> ComputeAdvisoryAsync(
        bool hasRoot, string root, IReadOnlyList<Game> selectedGames, IReadOnlyList<Game> changedGames) =>
        Task.Run(() =>
        {
            string? free = null;
            if (hasRoot && _backup.TryGetAvailableFreeSpace(root, out var freeBytes))
                free = Strings.Format("str_freeSpace_available", SizeFormat.Describe(freeBytes));

            // Memoized per game: a game present in both lists (selected while also having unsaved
            // changes, the common case) must have its save folder walked only once.
            var sizeByGame = new Dictionary<Game, long>();
            long SizeOf(Game game) =>
                sizeByGame.TryGetValue(game, out var size) ? size : sizeByGame[game] = _backup.ComputeSaveDataSize(game);

            string? selected = selectedGames.Count == 0
                ? null
                : Strings.Format("str_freeSpace_selectedNeeds",
                    SizeFormat.Describe(selectedGames.Sum(SizeOf)));

            string? changed = changedGames.Count == 0
                ? null
                : Strings.Format("str_freeSpace_changedNeeds",
                    SizeFormat.Describe(changedGames.Sum(SizeOf)));

            return (free, selected, changed);
        });
}
