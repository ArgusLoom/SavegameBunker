using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.ViewModels;

/// <summary>
/// The bulk backup and bulk restore runs behind <see cref="MainViewModel"/>'s toolbar:
/// "Back up all changed", "Back up selected" and "Restore selected" share this one orchestrator,
/// which claims every target row, runs the loop (backup in one <see cref="Task.Run(Action)"/>, restore
/// sequentially on the caller's own context) and releases every target row again in a
/// <c>finally</c>.
/// </summary>
/// <remarks>
/// <c>internal</c> and held only as a private readonly field on <see cref="MainViewModel"/> —
/// never as a public property — so nothing in XAML can bind a path to it. It references no WPF
/// type and holds no observable state: <see cref="MainViewModel"/> keeps every bound property
/// (the bulk counters, the restore-sentence flag, each row's progress fields) and constructs
/// every <c>Progress&lt;T&gt;</c>
/// on the UI thread and hands it in — this class never builds one, so every progress callback
/// lands on the UI thread in the order it was reported.
/// <para>
/// The claim and release order below is the re-entrancy guard: reset every target to
/// <see cref="BackupComparisonKind.Checking"/>, then <c>onStarted</c> (which raises
/// the bulk counters), then <c>IsBusy</c> on every target — and the release in the
/// <c>finally</c> reverses it before <c>onFinished</c> runs. Reordering or
/// short-circuiting any of those steps opens a window in which a second bulk run or a per-row
/// action can start on a row this run is about to write. <see cref="MainViewModel.IsAnyRowChecking"/> stays MainViewModel's aggregate —
/// this class only writes row comparisons, never the window-level guard itself.
/// </para>
/// </remarks>
internal sealed class BulkBackupCoordinator
{
    private readonly BackupService _backup;
    private readonly GameActions _gameActions;
    private readonly Action<Action> _onUi;

    public BulkBackupCoordinator(BackupService backup, GameActions gameActions, Action<Action> onUi)
    {
        _backup = backup;
        _gameActions = gameActions;
        _onUi = onUi;
    }

    /// <summary>
    /// Runs the shared bulk-backup loop over <paramref name="targets"/> in input order, one row
    /// at a time, and returns the per-game failures. Returns null for an empty target list
    /// before touching anything — the view model already guards, this keeps the coordinator
    /// total.
    /// </summary>
    /// <param name="onItem">
    /// Called at the top of each iteration with the 1-based index and the display name, invoked
    /// through <c>onUi</c> like every other row write in this loop — the same callback shape
    /// <see cref="RestoreAsync"/> already uses. Because it runs through <c>onUi</c> before that
    /// row's write begins, the view model's counters move on the UI thread before the row's
    /// write starts, and no report can arrive after the run ends.
    /// </param>
    /// <param name="byteProgress">
    /// One per-row byte reporter per target, each constructed by the caller on the UI thread.
    /// A missing entry for a target makes the loop throw — the caller always builds one per
    /// target, and the finally still releases everything when it happens.
    /// </param>
    /// <param name="onStarted">
    /// Called once, after the Checking reset and before any row is claimed, with the target
    /// count and the first target's display name — the caller raises its bulk counters here.
    /// </param>
    /// <param name="onFinished">
    /// Called exactly once from the finally, after every target has been released — the caller
    /// clears its counters and re-scans the targets there.
    /// </param>
    public async Task<BulkBackupRun?> BackupAsync(
        IReadOnlyList<GameRowViewModel> targets,
        string backupRoot,
        Action<int, string> onItem,
        IReadOnlyDictionary<GameRowViewModel, IProgress<long>> byteProgress,
        Action<int, string> onStarted,
        Action onFinished)
    {
        if (targets.Count == 0)
            return null;

        // Reset to Checking up front: this is both the visible "in progress" state for the rows
        // this run touches and the re-entrancy guard — the view model's IsAnyRowChecking flips
        // true immediately, disabling the bulk commands until the closing re-scan settles
        // everything again. Also bumps each row's ScanToken, the same invalidation
        // StatusSweepCoordinator.Reset uses, so a sweep's WalkAsync callback still in flight for a
        // target row can no longer match and overwrite this run's Checking display.
        foreach (var row in targets)
        {
            row.ScanToken++;
            row.ApplyBackupComparison(BackupComparison.Checking());
        }

        onStarted(targets.Count, targets[0].DisplayName);

        // Every target is marked busy for the whole run, not just while its own archive is being
        // written: the row's own Back up and Restore must not start on a row this loop is going
        // to write to. Bulk backup touches every selected row, so every selected row is
        // unavailable.
        foreach (var row in targets)
            row.IsBusy = true;

        var failed = new List<string>();
        var failureDetails = new List<string>();

        try
        {
            await Task.Run(() =>
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var row = targets[i];
                    var index = i + 1;
                    _onUi(() => onItem(index, row.DisplayName));

                    // Set before this row's write starts, same as the per-row BackupAsync command —
                    // the determinate-vs-indeterminate decision is made before the first byte.
                    var size = _backup.ComputeSaveDataSize(row.Game, backupRoot);
                    _onUi(() =>
                    {
                        row.BackupBytesTotal = size;
                        row.BackupBytesWritten = 0;
                    });

                    var result = _backup.Backup(row.Game, backupRoot, byteProgress[row]);
                    if (!result.Success)
                    {
                        failed.Add(row.DisplayName);
                        failureDetails.Add(result.Message);
                    }
                    else
                    {
                        // Wrapped like every other row write in this loop, since the bound row
                        // property must not be touched off the UI thread.
                        _onUi(() => row.RestoredArchiveTakenAt = null);
                    }

                    // Cleared the same way the per-row BackupAsync command clears it — the bar never
                    // outlives this row's own write, whatever the run's overall outcome.
                    _onUi(() =>
                    {
                        row.BackupBytesTotal = 0;
                        row.BackupBytesWritten = 0;
                    });
                }
            });
        }
        finally
        {
            // The finally is what keeps an unexpected failure inside the loop from leaving every
            // target row busy — a state that gates closing the window. IsBusy false and both
            // byte counters zero per target; the caller clears its own counters and rescans in
            // onFinished.
            foreach (var row in targets)
            {
                row.IsBusy = false;
                row.BackupBytesTotal = 0;
                row.BackupBytesWritten = 0;
            }

            onFinished();
        }

        return new BulkBackupRun(failed, failureDetails);
    }

    /// <summary>
    /// Runs the shared bulk-restore loop over <paramref name="targets"/> in input order,
    /// sequentially on the caller's own context — never one <see cref="Task.Run(Action)"/> around the
    /// loop, because the per-game restore confirmation needs the dispatcher. Returns the counts
    /// and per-game failures the closing outcome is composed from; null for an empty target
    /// list before touching anything.
    /// </summary>
    /// <param name="stepProgress">
    /// One per-row step reporter per target, each constructed by the caller on the UI thread.
    /// </param>
    /// <param name="onStarted">
    /// Called once, after the Checking reset and before any row is claimed — the caller raises
    /// its bulk counters (with the restore sentence) here.
    /// </param>
    /// <param name="onItem">
    /// Called at the top of each iteration with the 1-based index and the display name, before
    /// that game's own work starts.
    /// </param>
    /// <param name="onFinished">
    /// Called exactly once from the finally, after every target has been released — the caller
    /// clears its counters and re-scans the targets there, so a loop that threw cannot leave rows
    /// on Checking. The restore finally does not touch the byte counters: a restore never writes
    /// them.
    /// </param>
    public async Task<BulkRestoreRun?> RestoreAsync(
        IReadOnlyList<GameRowViewModel> targets,
        string backupRoot,
        IReadOnlyDictionary<GameRowViewModel, IProgress<int>> stepProgress,
        Action<int, string> onStarted,
        Action<int, string> onItem,
        Action onFinished)
    {
        if (targets.Count == 0)
            return null;

        // The same Checking reset, counter raise (via onStarted) and IsBusy claim order as the
        // backup path — the re-entrancy guard is one sequence shared by both bulk kinds. Also
        // bumps ScanToken for the same reason as BackupAsync's reset.
        foreach (var row in targets)
        {
            row.ScanToken++;
            row.ApplyBackupComparison(BackupComparison.Checking());
        }

        onStarted(targets.Count, targets[0].DisplayName);

        foreach (var row in targets)
            row.IsBusy = true;

        var failed = new List<string>();
        var failureDetails = new List<string>();
        var declined = new List<string>();
        var restored = 0;

        try
        {
            // Sequential, on the caller's own context — never wrapped in one
            // Task.Run, because the per-game Confirm needs the dispatcher: the confirmation is a
            // blocking wait for a dialog whose pump lives on the UI thread.
            for (var i = 0; i < targets.Count; i++)
            {
                var row = targets[i];
                onItem(i + 1, row.DisplayName);

                var (plan, error) = await _gameActions.TryPlanNewestOrdinaryAsync(row, backupRoot);
                if (error is not null)
                {
                    failed.Add(row.DisplayName);
                    failureDetails.Add(error.Message);
                    continue;
                }

                var result = await _gameActions.RestoreAsync(row, plan!, stepProgress[row], i + 1, targets.Count);
                row.RestoreStep = 0;
                row.RestoreStepText = "";

                if (result.Cancelled)
                {
                    declined.Add(row.DisplayName);
                    continue;
                }

                if (result.Success)
                {
                    restored++;
                    row.RestoredArchiveTakenAt = plan!.Picked.TakenAt;
                }
                else
                {
                    failed.Add(row.DisplayName);
                    failureDetails.Add(result.Message);
                }
            }
        }
        finally
        {
            // Release before the caller's onFinished, as in the backup path. No byte counters to
            // clear: a restore never writes them. The step display is cleared here too, so a row
            // the loop threw on does not keep a frozen restore bar.
            foreach (var row in targets)
            {
                row.IsBusy = false;
                row.RestoreStep = 0;
                row.RestoreStepText = "";
            }

            onFinished();
        }

        return new BulkRestoreRun(restored, failed, failureDetails, declined);
    }
}

/// <summary>
/// What one bulk backup run produced: the display names of the games whose backup failed, and
/// one failure sentence per failed game for the blocking dialog.
/// </summary>
internal sealed record BulkBackupRun(IReadOnlyList<string> Failed, IReadOnlyList<string> FailureDetails);

/// <summary>
/// What one bulk restore run produced: how many games were actually restored, the display names
/// of the failed and declined games, and one failure sentence per failed game for the blocking
/// dialog.
/// </summary>
internal sealed record BulkRestoreRun(int Restored, IReadOnlyList<string> Failed, IReadOnlyList<string> FailureDetails, IReadOnlyList<string> Declined);
