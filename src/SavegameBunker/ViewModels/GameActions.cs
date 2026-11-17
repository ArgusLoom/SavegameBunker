using System.Globalization;
using System.Linq;
using SavegameBunker.Converters;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.ViewModels;

/// <summary>
/// Per-game backup and restore orchestration — the largest single flow
/// <see cref="MainViewModel"/> owned before this extraction: the three pre-flight
/// refusals, the restore picker, the single pre-restore save scan, the restore confirmation and
/// the two service calls that write to disk.
/// </summary>
/// <remarks>
/// <c>internal</c> and held only as a private readonly field on <see cref="MainViewModel"/> —
/// never as a public property — so nothing in XAML can bind a path to it and nothing outside the
/// assembly can reach the restore runner. It references no WPF type and holds no
/// observable state.
/// <para>
/// Constructs its own <see cref="RestoreService"/> from the <see cref="BackupService"/> it is
/// given, rather than taking one as a third constructor parameter: <see cref="MainViewModel"/>
/// still needs <see cref="BackupService"/> for its own status sweep, but has no other use for a
/// <see cref="RestoreService"/> once this class owns every call into it, and this keeps that
/// construction — and the type name itself — out of <see cref="MainViewModel"/> entirely, which
/// is what makes "no direct catalog-service call" and "single writer" structural rather than a
/// convention the next edit could quietly break.
/// </para>
/// </remarks>
internal sealed class GameActions
{
    private readonly BackupService _backup;
    private readonly RestoreService _restore;
    private readonly IDialogService _dialog;

    public GameActions(BackupService backup, IDialogService dialog)
    {
        _backup = backup;
        _restore = new RestoreService(backup);
        _dialog = dialog;
    }

    /// <summary>
    /// Writes a single-game backup to the resolved <paramref name="backupRoot"/>. Resolving or
    /// prompting for a folder stays with the caller — that is a catalog gesture, and the bulk
    /// backup command reuses this same write for every row it touches.
    /// </summary>
    /// <param name="progress">
    /// Optional cumulative-bytes-archived reporter, carried straight through to
    /// <see cref="BackupService.Backup(Game, string, IProgress{long}?)"/>. This method adds no
    /// progress state of its own — the view model owns the bar.
    /// </param>
    public Task<OperationResult> BackupAsync(GameRowViewModel row, string backupRoot, IProgress<long>? progress = null) =>
        Task.Run(() => _backup.Backup(row.Game, backupRoot, progress));

    /// <summary>
    /// The three pre-flight refusals, in their existing fixed order: folder resolvable,
    /// then reachable, then a non-empty archive list — so a game with no backups under an
    /// unreachable folder still reports the folder, which is the problem the user can act on.
    /// Returns null when the row may proceed to the picker.
    /// </summary>
    /// <param name="configuredBackupRoot">
    /// The resolved backup folder, or null when none is configured — the caller has already
    /// applied the catalog's own resolvable-and-non-blank check.
    /// </param>
    public async Task<OperationResult?> CheckRestorePreconditionsAsync(GameRowViewModel row, string? configuredBackupRoot)
    {
        if (configuredBackupRoot is null)
        {
            return OperationResult.Fail(Strings.Format("str_restore_noBackupFolder", row.DisplayName));
        }

        var (reachable, listable, backups) = await Task.Run(() =>
        {
            if (!_backup.IsBackupRootReachable(configuredBackupRoot))
            {
                return (false, false, (IReadOnlyList<BackupArchive>)Array.Empty<BackupArchive>());
            }

            var ok = _backup.TryListBackups(configuredBackupRoot, row.Game.Id, out var found);
            return (true, ok, found);
        });

        if (!reachable)
        {
            return OperationResult.Fail(Strings.Format("str_restore_folderUnreachable", row.DisplayName, configuredBackupRoot));
        }

        if (!listable)
        {
            return OperationResult.Fail(Strings.Format("str_restore_folderUnreachable", row.DisplayName, configuredBackupRoot));
        }

        if (backups.Count == 0)
        {
            return OperationResult.Fail(Strings.Format("str_restore_noBackups", row.DisplayName));
        }

        return null;
    }

    /// <summary>
    /// Opens the restore picker and, once an archive is picked, runs the single pre-restore save
    /// scan — once, before the restore, so the confirmation's warning decision and its elapsed
    /// value come from a single reading rather than a second timestamp rule written inline
    /// elsewhere. Returns null when the user cancelled the picker.
    /// </summary>
    public async Task<RestorePlan?> PickRestoreAsync(GameRowViewModel row, string backupRoot)
    {
        // CheckRestorePreconditionsAsync already confirmed the folder was listable and non-empty —
        // re-checking here closes the TOCTOU window between that check and this call (the folder
        // could stop being listable in between) rather than showing the picker with a list that
        // silently came back empty. Folded into the same "nothing to restore" outcome as a
        // cancelled pick: this method has no error channel of its own, and the gap is rare enough
        // that reusing the existing null-means-nothing-happened contract beats widening the
        // signature for it.
        var listable = await Task.Run(() => _backup.TryListBackups(backupRoot, row.Game.Id, out var found) ? found : null);
        if (listable is null)
            return null;

        var picked = _dialog.ShowRestorePickerDialog(row.DisplayName, listable);
        if (picked is null)
            return null;

        var status = await Task.Run(() => SaveStatusScanner.Scan(row.Game));
        return new RestorePlan(picked, status, backupRoot);
    }

    /// <summary>
    /// Builds a <see cref="RestorePlan"/> from the newest ordinary archive — no picker.
    /// Safety archives stay listed for single-row restore; bulk never auto-picks them.
    /// </summary>
    public async Task<(RestorePlan? Plan, OperationResult? Error)> TryPlanNewestOrdinaryAsync(
        GameRowViewModel row, string backupRoot)
    {
        var (listable, backups, status) = await Task.Run(() =>
        {
            var ok = _backup.TryListBackups(backupRoot, row.Game.Id, out var found);
            return (ok, found, SaveStatusScanner.Scan(row.Game));
        });

        if (!listable)
        {
            return (null, OperationResult.Fail(
                Strings.Format("str_restore_folderUnreachable", row.DisplayName, backupRoot)));
        }

        var ordinary = backups.FirstOrDefault(archive => !archive.IsSafetyBackup);
        if (ordinary is null)
        {
            return (null, OperationResult.Fail(
                Strings.Format("str_restore_noOrdinaryBackup", row.DisplayName)));
        }

        return (new RestorePlan(ordinary, status, backupRoot), null);
    }

    /// <summary>
    /// Runs the restore itself: the confirmation, built from <paramref name="plan"/>,
    /// composed here and shown through <see cref="RestoreService"/>'s confirmation delegate.
    /// </summary>
    /// <param name="progress">
    /// Optional step reporter, carried straight through to
    /// <see cref="RestoreService.Restore"/>, which is the only place the three step boundaries are
    /// observable. This method adds no progress state of its own — the view model owns the bar.
    /// </param>
    public Task<OperationResult> RestoreAsync(
        GameRowViewModel row, RestorePlan plan, IProgress<int>? progress = null,
        int? bulkIndex = null, int? bulkTotal = null) =>
        Task.Run(() => _restore.Restore(
            row.Game, plan.BackupRoot, plan.Picked.FilePath,
            prompt => _dialog.Confirm(
                BuildConfirmation(row, plan.Picked, plan.Status, prompt, bulkIndex, bulkTotal),
                Strings.Get("str_common_restore")),
            progress));

    /// <summary>
    /// Renders elapsed time the same way every bound timestamp in the app does, for the one
    /// surface that cannot bind a converter (the restore confirmation is a plain string, not a XAML
    /// element) — the "relative, exact on hover" rule adapts to "relative inline, exact
    /// inline" here. Calls the ported converter's
    /// existing method directly rather than duplicating its threshold ladder.
    /// </summary>
    private static string RelativeTime(DateTime value) =>
        (string)new RelativeTimeConverter().Convert(value, typeof(string), null, CultureInfo.CurrentCulture);

    /// <summary>
    /// Builds the restore confirmation shown after the safety backup has been written (or found
    /// unnecessary) and before anything about the live save directory changes. Reuses
    /// <see cref="BackupComparer"/>/<see cref="ElapsedFormat"/> for the warning paragraph rather
    /// than comparing two <see cref="DateTime"/> values inline, so this prompt and the row's own
    /// Status cell can never disagree about which side is newer — which is also the
    /// E9 backstop this method exists to prove: the two paragraphs below can never both appear,
    /// because the warning requires <see cref="BackupComparisonKind.SavesChangedSinceBackup"/>,
    /// which itself requires a scanned save timestamp newer than the archive, while the
    /// no-current-saves paragraph fires only when <paramref name="prompt"/> found nothing to back
    /// up — the two conditions describe mutually exclusive states of the same save directory at
    /// the same instant.
    /// </summary>
    private static string BuildConfirmation(
        GameRowViewModel row, BackupArchive picked, SaveStatus status, RestorePrompt prompt,
        int? bulkIndex = null, int? bulkTotal = null)
    {
        var opening = bulkIndex is int index && bulkTotal is int total
            ? Strings.Format("str_restoreConfirm_openingBulk", row.DisplayName, index, total, RelativeTime(picked.TakenAt))
            : Strings.Format("str_restoreConfirm_opening", row.DisplayName, RelativeTime(picked.TakenAt));

        var paragraphs = new List<string>
        {
            opening,
            Strings.Format("str_restoreConfirm_saveFolder", prompt.SaveDir) + "\n" + (prompt.SafetyBackupPath is not null
                ? Strings.Get("str_restoreConfirm_safetyTaken")
                : Strings.Get("str_restoreConfirm_safetyNone")),
        };

        var comparison = BackupComparer.Compare(status, backupRootReachable: true, new[] { picked });
        if (comparison.Kind == BackupComparisonKind.SavesChangedSinceBackup)
        {
            paragraphs.Add(Strings.Format("str_restoreConfirm_warning", ElapsedFormat.Describe(comparison.Elapsed!.Value)));
        }

        return string.Join("\n\n", paragraphs);
    }
}

/// <summary>
/// The three values <see cref="GameActions.RestoreAsync"/> needs to run a restore: the picked
/// archive, the pre-restore save status and the resolved backup folder.
/// </summary>
internal sealed record RestorePlan(BackupArchive Picked, SaveStatus Status, string BackupRoot);
