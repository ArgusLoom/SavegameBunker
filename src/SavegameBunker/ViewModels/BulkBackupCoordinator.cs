using System.Linq;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.ViewModels;

/// <summary>
/// The bulk backup run <see cref="MainViewModel"/> used to hold inline: "Back up all changed"
/// and "Back up selected" share this one orchestrator, which claims every target row, runs the
/// backup loop in one <see cref="Task.Run"/> and releases every target row again in a
/// <c>finally</c>.
/// </summary>
/// <remarks>
/// <c>internal</c> and held only as a private readonly field on <see cref="MainViewModel"/> —
/// never as a public property — so nothing in XAML can bind a path to it. It references no WPF
/// type and holds no observable state: <see cref="MainViewModel"/> keeps every bound property
/// (the bulk counters, each row's progress fields) and constructs every <c>Progress&lt;T&gt;</c>
/// on the UI thread and hands it in — this class never builds one, so callback ordering matches
/// the pre-split single-owner behavior exactly.
/// <para>
/// The claim and release order below is the REF-03 re-entrancy guard: reset every target to
/// <see cref="BackupComparisonKind.Checking"/>, then <paramref name="onStarted"/> (which raises
/// the bulk counters), then <c>IsBusy</c> on every target — and the release in the
/// <c>finally</c> reverses it before <paramref name="onFinished"/> runs. Reordering or
/// short-circuiting any of those steps would re-open the re-entrancy window the original inline
/// order closed. <see cref="MainViewModel.IsAnyRowChecking"/> stays MainViewModel's aggregate —
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
    /// <param name="progress">
    /// The (index, display-name) reporter for the window-level progress sentence, constructed by
    /// the caller on the UI thread.
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
        IProgress<(int Index, string Name)> progress,
        IReadOnlyDictionary<GameRowViewModel, IProgress<long>> byteProgress,
        Action<int, string> onStarted,
        Action onFinished)
    {
        if (targets.Count == 0)
            return null;

        // Reset to Checking up front: this is both the visible "in progress" state for the rows
        // this run touches and the re-entrancy guard — the view model's IsAnyRowChecking flips
        // true immediately, disabling the bulk commands until the closing re-scan settles
        // everything again.
        foreach (var row in targets)
            row.ApplyBackupComparison(BackupComparison.Checking());

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
                    progress.Report((i + 1, row.DisplayName));

                    // Set before this row's write starts, same as the per-row BackupAsync command —
                    // the determinate-vs-indeterminate decision is made before the first byte.
                    var size = _backup.ComputeSaveDataSize(row.Game);
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
                        // property must not be touched off the UI thread (audit A9).
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
}

/// <summary>
/// What one bulk backup run produced: the display names of the games whose backup failed, and
/// one failure sentence per failed game for the blocking dialog.
/// </summary>
internal sealed record BulkBackupRun(IReadOnlyList<string> Failed, IReadOnlyList<string> FailureDetails);
