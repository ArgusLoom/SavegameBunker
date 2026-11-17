using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.ViewModels;

/// <summary>
/// The view-model state the per-row flows may touch, and nothing else (D-P05-02). The flows
/// interleave nine touches on <see cref="MainViewModel"/> state — backup root, configured-root
/// check, status line, report, re-scan, and the two in-flight counters behind
/// <c>IsBackupRunning</c>/<c>IsRestoreRunning</c> — so this one internal contract replaces what
/// would otherwise be a nine-parameter delegate signature. Implemented explicitly by
/// <see cref="MainViewModel"/>, so no public member is added.
/// </summary>
internal interface IRowActionHost
{
    Task<string?> EnsureBackupRootAsync();
    bool TryGetConfiguredBackupRoot(out string root);
    void SetStatus(string message);
    void Report(OperationResult result);
    void Rescan(GameRowViewModel row);
    void BackupStarted();
    void BackupFinished();
    void RestoreStarted();
    void RestoreFinished();
}

/// <summary>
/// The per-row Back up, Restore and Open save folder flows <see cref="MainViewModel"/> used to
/// hold inline: each reads a service answer, writes that row's progress fields and reports the
/// outcome. <see cref="MainViewModel"/> builds each row's <c>Progress&lt;T&gt;</c> on the UI
/// thread and hands it in — this class never builds one.
/// </summary>
/// <remarks>
/// <c>internal</c> and held only as a private readonly field on <see cref="MainViewModel"/> —
/// never as a public property — so nothing in XAML can bind a path to it. It references no WPF
/// type and holds no observable state. The row's own <c>IsBusy</c> is raised before the first
/// await and released in a <c>finally</c>: that is the per-row re-entrancy guard, and its
/// statement order below is the pre-split order, moved verbatim.
/// </remarks>
internal sealed class RowActionsCoordinator
{
    private readonly BackupService _backup;
    private readonly GameActions _gameActions;
    private readonly IDialogService _dialog;
    private readonly IRowActionHost _host;

    public RowActionsCoordinator(BackupService backup, GameActions gameActions, IDialogService dialog, IRowActionHost host)
    {
        _backup = backup;
        _gameActions = gameActions;
        _dialog = dialog;
        _host = host;
    }

    /// <summary>
    /// Writes a backup and, on success, re-runs the status sweep: a new
    /// archive can change this row's save-vs-backup verdict, and a failed backup changed nothing
    /// worth re-scanning for.
    /// </summary>
    public async Task BackupAsync(GameRowViewModel? row, IProgress<long>? progress)
    {
        if (row is null || row.IsBusy)
            return;

        // row.IsBusy is this row's own re-entrancy guard and is raised immediately, before
        // EnsureBackupRootAsync's await, so a second click landing before that await returns
        // (backup root still unset) cannot slip through and start a second backup for this row.
        row.IsBusy = true;
        try
        {
            var root = await _host.EnsureBackupRootAsync();
            if (root is null)
                return; // Either the user cancelled the picker or saving the choice failed — both routes already told the user what happened.

            _host.BackupStarted();
            try
            {
                _host.SetStatus(Strings.Format("str_backup_inProgress", row.DisplayName));

                // Set before the write starts so the determinate-vs-indeterminate decision
                // is already made by the time ArchiveSaveDir writes its first byte. Run off the UI
                // thread — it is a directory walk.
                row.BackupBytesTotal = await Task.Run(() => _backup.ComputeSaveDataSize(row.Game));
                row.BackupBytesWritten = 0;

                var result = await _gameActions.BackupAsync(row, root, progress);
                _host.Report(result);
                if (result.Success)
                {
                    row.RestoredArchiveTakenAt = null;
                    _host.Rescan(row);
                }
            }
            finally
            {
                _host.BackupFinished();
            }
        }
        finally
        {
            row.IsBusy = false;
            // The bar never outlives the write — cleared in the same finally as IsBusy,
            // mirroring how RestoreAsync clears RestoreStep.
            row.BackupBytesTotal = 0;
            row.BackupBytesWritten = 0;
        }
    }

    /// <summary>
    /// Delegates the pre-flight refusals, the picker, the pre-restore scan and the
    /// restore itself to <see cref="GameActions"/>, and owns the busy flag, the report and the
    /// re-sweep around them. <c>IsRestoreRunning</c> — the flag other commands gate on —
    /// is still raised only once a plan is in hand, never before: an unset backup folder here is
    /// a named error, not a prompt for a destination, and prompting for one is a backup gesture
    /// whose cancel wording is worded for a backup, not a restore. <c>row.IsBusy</c> is different:
    /// it is this row's own re-entrancy guard and is raised immediately, before the precondition
    /// check or the picker dialog run, so a second click on the same row while either is still
    /// pending cannot slip through and start two restores against the same save directory — the
    /// app's only destructive operation.
    /// </summary>
    public async Task RestoreAsync(GameRowViewModel? row, IProgress<int>? progress)
    {
        if (row is null || row.IsBusy)
            return;

        row.IsBusy = true;
        try
        {
            var hasRoot = _host.TryGetConfiguredBackupRoot(out var root);

            var refusal = await _gameActions.CheckRestorePreconditionsAsync(row, hasRoot ? root : null);
            if (refusal is not null)
            {
                _host.Report(refusal);
                return;
            }

            var plan = await _gameActions.PickRestoreAsync(row, root);
            if (plan is null)
            {
                // A cancelled picker is not a failure — no dialog, just the status line, matching
                // how ChangeBackupRootAsync treats a cancelled folder picker.
                _host.SetStatus(Strings.Get("str_restore_cancelled"));
                return;
            }

            _host.RestoreStarted();
            try
            {
                _host.SetStatus(Strings.Format("str_restore_inProgress", row.DisplayName));
                var result = await _gameActions.RestoreAsync(row, plan, progress);

                // Progress and outcome never share a surface: the bar is cleared before the
                // result reaches the transient one.
                row.RestoreStep = 0;
                row.RestoreStepText = "";

                if (result.Success)
                    row.RestoredArchiveTakenAt = plan.Picked.TakenAt;

                _host.Report(result);
                _host.Rescan(row);
            }
            finally
            {
                row.RestoreStep = 0;
                row.RestoreStepText = "";
                _host.RestoreFinished();
            }
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    /// <summary>
    /// Opens the row's resolved save folder in Explorer (row overflow). Reachable only from
    /// there. An unresolvable template reports the Game cell's own problem sentence rather than a
    /// second, differently-worded one for the same fact.
    /// </summary>
    public async Task OpenSaveFolderAsync(GameRowViewModel? row)
    {
        if (row is null)
            return;

        if (!PathResolver.TryResolve(row.Game.SaveDirTemplate, null, out var dir, out _))
        {
            _host.Report(OperationResult.Fail(Strings.Get("str_game_pathError")));
            return;
        }

        if (!Directory.Exists(dir))
        {
            _host.Report(OperationResult.Fail(Strings.Get("str_open_notAFolder")));
            return;
        }

        var result = await Task.Run(() => _dialog.OpenInEditor(dir));
        _host.Report(result);
    }
}
