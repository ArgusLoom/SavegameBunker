using System.Linq;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.ViewModels;

/// <summary>
/// The pure bulk-run rules every bulk path shares: the unsecured verdict pair
/// (<see cref="BackupComparisonKind.SavesChangedSinceBackup"/> and
/// <see cref="BackupComparisonKind.NeverBackedUp"/>), the two bulk target selections, and the
/// closing outcome composition of a bulk backup or bulk restore.
/// </summary>
/// <remarks>
/// <c>static</c> and <c>internal</c> because every member is a pure function of its arguments —
/// it references no WPF type, performs no I/O and holds no state — so each rule has exactly one
/// definition that the bulk coordinator, the space advisory and the startup prompt cannot drift
/// from.
/// </remarks>
internal static class BulkRunRules
{
    /// <summary>
    /// Whether a row's comparison verdict means its save data needs a backup: exactly
    /// <see cref="BackupComparisonKind.SavesChangedSinceBackup"/> and
    /// <see cref="BackupComparisonKind.NeverBackedUp"/>. The other four kinds are excluded
    /// because they cannot or need not be backed up — <see cref="BackupComparisonKind.Checking"/>
    /// is a half-finished sweep that must not define a selection,
    /// <see cref="BackupComparisonKind.UpToDate"/> already has a current archive,
    /// <see cref="BackupComparisonKind.NoSaveData"/> has nothing to back up, and
    /// <see cref="BackupComparisonKind.BackupFolderNotAvailable"/> cannot be judged at all.
    /// </summary>
    public static bool NeedsBackup(BackupComparisonKind kind) =>
        kind is BackupComparisonKind.SavesChangedSinceBackup or BackupComparisonKind.NeverBackedUp;

    /// <summary>
    /// The bulk-backup target list: every row whose verdict needs a backup and that is not
    /// already mid-flight on its own backup or restore — including a restore still in preflight,
    /// before IsRestoreRunning is raised — in input order. A busy row is excluded rather than
    /// re-claimed: IsAnyRowBusy only gates whether the command can start at all, which does not
    /// catch a row that went busy after the sweep last settled but before this run's own target
    /// list is built.
    /// </summary>
    public static List<GameRowViewModel> ChangedTargets(IEnumerable<GameRowViewModel> rows) =>
        [.. rows.Where(row => NeedsBackup(row.ComparisonKind) && !row.IsBusy)];

    /// <summary>
    /// The bulk target list for the selection-driven commands: every checked row that is not
    /// already busy, in input order. A checked game is an explicit per-game choice, so the
    /// comparison verdict deliberately does not filter here — the same busy exclusion applies as
    /// for the comparison-driven selection: a row already on its own backup or restore is
    /// excluded rather than re-claimed.
    /// </summary>
    public static List<GameRowViewModel> SelectedTargets(IEnumerable<GameRowViewModel> rows) =>
        [.. rows.Where(row => row.IsSelected && !row.IsBusy)];

    /// <summary>
    /// Composes the closing outcome of a bulk backup from its targets and failures: no failures and a
    /// single target names that one game; no failures and several names the caller's all-succeeded
    /// key with the count; otherwise the caller's partial-failure key names how many succeeded,
    /// how many were attempted, and the comma-joined failed game names.
    /// </summary>
    public static OperationResult BackupOutcome(
        IReadOnlyList<GameRowViewModel> targets,
        IReadOnlyCollection<string> failed,
        string allSucceededKey,
        string partialFailureKey)
    {
        return failed.Count == 0
            ? OperationResult.Ok(targets.Count == 1
                ? Strings.Format("str_bulkBackup_one", targets[0].DisplayName)
                : Strings.Format(allSucceededKey, targets.Count))
            : OperationResult.Fail(Strings.Format(
                partialFailureKey, targets.Count - failed.Count, targets.Count, string.Join(", ", failed)));
    }

    /// <summary>
    /// Composes the closing outcome of a bulk restore from its targets and results, in four
    /// branches: a cancel when nothing was
    /// restored and nothing failed (every game was either declined or not attempted); a failure
    /// result whenever anything failed — failure wins over a partial skip; a skip-naming result
    /// when some games were declined but none failed, naming how many actually were restored so
    /// the run never claims "all" over a declined game; and finally the single-game or all count.
    /// </summary>
    public static OperationResult RestoreOutcome(
        IReadOnlyList<GameRowViewModel> targets,
        int restored,
        IReadOnlyCollection<string> failed,
        IReadOnlyCollection<string> declined)
    {
        if (failed.Count == 0 && restored == 0)
            return OperationResult.Cancel(Strings.Get("str_restore_cancelled"));

        if (failed.Count > 0)
            return OperationResult.Fail(Strings.Format(
                "str_bulkRestoreSelected_partialFailure", restored, targets.Count, string.Join(", ", failed)));

        if (declined.Count > 0)
            return OperationResult.Ok(Strings.Format(
                "str_bulkRestoreSelected_partialSkipped", restored, targets.Count, string.Join(", ", declined)));

        return OperationResult.Ok(targets.Count == 1
            ? Strings.Format("str_bulkRestore_one", targets[0].DisplayName)
            : Strings.Format("str_bulkRestoreSelected_allSucceeded", targets.Count));
    }
}
