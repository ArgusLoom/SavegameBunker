using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Computes one row's save-vs-backup verdict from data the caller has already
/// fetched. Pure and static: no filesystem access, no field, no state to hold — <see cref="Compare"/>
/// is safe to call from any number of rows at once inside the existing bounded sweep.
/// </summary>
public static class BackupComparer
{
    /// <summary>
    /// Compares <paramref name="saveStatus"/> against the newest of <paramref name="backups"/>,
    /// gated first by <paramref name="backupRootReachable"/> (an unreachable backup
    /// folder means the comparison genuinely cannot be made, whatever the save side looks like).
    ///
    /// <see cref="SaveStatusKind.NoSaves"/>, <see cref="SaveStatusKind.PathError"/> and the rare
    /// <see cref="SaveStatusKind.Found"/>-with-null-<c>LastModified</c> case all fold into
    /// <see cref="BackupComparisonKind.NoSaveData"/> rather than becoming a sixth kind: the verdict set names
    /// five outcomes, and the Game column's existing <c>HasPathError</c> styling already names a
    /// path error distinctly, so a second and possibly contradictory sentence in this column would
    /// be worse than sharing this bucket.
    /// </summary>
    public static BackupComparison Compare(
        SaveStatus saveStatus,
        bool backupRootReachable,
        IReadOnlyList<BackupArchive> backups,
        DateTime? restoredArchiveTakenAt = null)
    {
        if (!backupRootReachable)
            return BackupComparison.FolderNotAvailable();

        // ListBackups already guarantees newest-first; reading position 0 is the only
        // ordering rule this method needs — re-sorting here would be a second, drifting rule.
        var newestTakenAt = backups.Count > 0 ? backups[0].TakenAt : (DateTime?)null;

        if (saveStatus.Kind == SaveStatusKind.Checking)
            return BackupComparison.Checking();

        if (saveStatus.Kind != SaveStatusKind.Found || saveStatus.LastModified is not { } lastModified)
            return BackupComparison.NoSaveData(newestTakenAt);

        if (backups.Count == 0)
            return BackupComparison.NeverBackedUp();

        // A restore replaces the save directory's content (and its mtime) with an older archive's
        // own content. The plain "newest backup at or after the save's mtime" rule below reads
        // that as up to date — the fresh pre-restore safety backup it just made is newer than the
        // restored archive's old mtime — even though that safety backup holds the PREVIOUS,
        // different content, not the one now on disk. restoredArchiveTakenAt names which archive
        // disk content actually matches; whenever that is not the newest one, the two have
        // genuinely diverged, whatever the timestamps alone would suggest.
        if (restoredArchiveTakenAt is { } restoredAt && restoredAt != newestTakenAt!.Value)
            return BackupComparison.SavesChangedSinceBackup(newestTakenAt.Value, newestTakenAt.Value - restoredAt);

        // At-or-after counts as up to date.
        return newestTakenAt!.Value >= lastModified
            ? BackupComparison.UpToDate(newestTakenAt.Value)
            : BackupComparison.SavesChangedSinceBackup(newestTakenAt.Value, lastModified - newestTakenAt.Value);
    }
}
