namespace SavegameBunker.Models;

/// <summary>
/// The five save-vs-backup verdicts, plus the transient scan-in-flight state.
/// </summary>
public enum BackupComparisonKind
{
    /// <summary>The background comparison sweep for this row has not reported yet.</summary>
    Checking,

    /// <summary>The newest backup was taken at or after the save directory's last change.</summary>
    UpToDate,

    /// <summary>The save directory has changed since the newest backup was taken.</summary>
    SavesChangedSinceBackup,

    /// <summary>There is save data, but no backup exists for this game yet.</summary>
    NeverBackedUp,

    /// <summary>The save directory is missing or empty, cannot be resolved, or the scan errored.</summary>
    NoSaveData,

    /// <summary>The configured backup folder cannot currently be reached, so no comparison can be made.</summary>
    BackupFolderNotAvailable,
}

/// <summary>
/// Result of comparing one row's save directory against its newest backup.
/// </summary>
/// <remarks>
/// <see cref="NewestBackupTakenAt"/> is populated whenever a backup exists, independently of
/// <see cref="Kind"/> — including on the <see cref="BackupComparisonKind.NoSaveData"/> branch. A
/// player whose save folder is empty is precisely the player who most needs to see that an archive
/// from three days ago exists; blanking that column there would hide the one fact that makes
/// restoring actionable.
///
/// Both <see cref="NewestBackupTakenAt"/> and the save directory's own last-modified time are local
/// wall-clock values with no timezone offset attached. An archive carried over from another machine,
/// or written under a wrong system clock, can invert the verdict this record carries. That is an
/// accepted limitation, not a bug to fix here: the comparison informs a sentence and never gates an
/// operation, so an inverted verdict costs a confusing sentence and never data.
/// </remarks>
public sealed record BackupComparison(
    BackupComparisonKind Kind,
    DateTime? NewestBackupTakenAt,
    TimeSpan? Elapsed,
    bool BackupRootReachable)
{
    public static BackupComparison Checking() =>
        new(BackupComparisonKind.Checking, null, null, true);

    public static BackupComparison UpToDate(DateTime newestTakenAt) =>
        new(BackupComparisonKind.UpToDate, newestTakenAt, null, true);

    public static BackupComparison SavesChangedSinceBackup(DateTime newestTakenAt, TimeSpan elapsed) =>
        new(BackupComparisonKind.SavesChangedSinceBackup, newestTakenAt, elapsed, true);

    public static BackupComparison NeverBackedUp() =>
        new(BackupComparisonKind.NeverBackedUp, null, null, true);

    public static BackupComparison NoSaveData(DateTime? newestTakenAt) =>
        new(BackupComparisonKind.NoSaveData, newestTakenAt, null, true);

    public static BackupComparison FolderNotAvailable() =>
        new(BackupComparisonKind.BackupFolderNotAvailable, null, null, false);
}
