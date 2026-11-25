using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// <see cref="BackupComparer.Compare"/> is pure: every input is constructed directly, no temp
/// directory, no filesystem, no timing — the same no-fixture shape <see cref="SaveStatusScannerTests"/>
/// uses for a pure classification function.
/// </summary>
public sealed class BackupComparerTests
{
    private static BackupArchive Archive(DateTime takenAt, string fileName = "a.zip") =>
        new(fileName, takenAt, 100);

    [Fact]
    public void Unreachable_backup_folder_returns_BackupFolderNotAvailable_even_with_found_saves_and_backups()
    {
        var saveStatus = SaveStatus.Found(3, 300, DateTime.Now, oversized: false);
        var backups = new[] { Archive(DateTime.Now) };

        var comparison = BackupComparer.Compare(saveStatus, backupRootReachable: false, backups);

        Assert.Equal(BackupComparisonKind.BackupFolderNotAvailable, comparison.Kind);
    }

    [Fact]
    public void Found_saves_with_a_strictly_later_backup_returns_UpToDate_and_carries_its_TakenAt()
    {
        var lastModified = new DateTime(2026, 8, 1, 12, 0, 0);
        var takenAt = lastModified.AddMinutes(5);
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);

        var comparison = BackupComparer.Compare(saveStatus, true, [Archive(takenAt)]);

        Assert.Equal(BackupComparisonKind.UpToDate, comparison.Kind);
        Assert.Equal(takenAt, comparison.NewestBackupTakenAt);
    }

    /// <summary>
    /// A -partial archive (its backup skipped unreadable files) never makes the game up to date:
    /// the verdict comes from the newest complete archive, or NeverBackedUp when there is none.
    /// </summary>
    [Fact]
    public void A_newer_partial_archive_does_not_make_the_game_up_to_date()
    {
        var lastModified = new DateTime(2026, 8, 1, 12, 0, 0);
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);
        var partial = Archive(lastModified.AddMinutes(5), "2026-08-01_120500-partial.zip");
        var older = Archive(lastModified.AddDays(-1), "2026-07-31_120000.zip");

        Assert.Equal(BackupComparisonKind.SavesChangedSinceBackup,
            BackupComparer.Compare(saveStatus, true, [partial, older]).Kind);
        Assert.Equal(BackupComparisonKind.NeverBackedUp,
            BackupComparer.Compare(saveStatus, true, [partial]).Kind);
    }

    [Fact]
    public void Found_saves_with_a_backup_taken_at_exactly_LastModified_returns_UpToDate()
    {
        var lastModified = new DateTime(2026, 8, 1, 12, 0, 0);
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);

        var comparison = BackupComparer.Compare(saveStatus, true, [Archive(lastModified)]);

        Assert.Equal(BackupComparisonKind.UpToDate, comparison.Kind);
    }

    [Fact]
    public void Found_saves_with_a_backup_one_tick_earlier_returns_SavesChangedSinceBackup_with_elapsed()
    {
        var lastModified = new DateTime(2026, 8, 1, 12, 0, 0);
        var takenAt = lastModified.AddTicks(-1);
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);

        var comparison = BackupComparer.Compare(saveStatus, true, [Archive(takenAt)]);

        Assert.Equal(BackupComparisonKind.SavesChangedSinceBackup, comparison.Kind);
        Assert.Equal(takenAt, comparison.NewestBackupTakenAt);
        Assert.Equal(lastModified - takenAt, comparison.Elapsed);
    }

    [Fact]
    public void Found_saves_with_an_empty_backup_list_returns_NeverBackedUp_with_no_newest_timestamp()
    {
        var saveStatus = SaveStatus.Found(1, 10, DateTime.Now, oversized: false);

        var comparison = BackupComparer.Compare(saveStatus, true, []);

        Assert.Equal(BackupComparisonKind.NeverBackedUp, comparison.Kind);
        Assert.Null(comparison.NewestBackupTakenAt);
    }

    [Fact]
    public void NoSaves_with_an_empty_backup_list_returns_NoSaveData_with_no_newest_timestamp()
    {
        var comparison = BackupComparer.Compare(SaveStatus.NoSaves(), true, []);

        Assert.Equal(BackupComparisonKind.NoSaveData, comparison.Kind);
        Assert.Null(comparison.NewestBackupTakenAt);
    }

    [Fact]
    public void NoSaves_with_a_backup_available_returns_NoSaveData_but_still_carries_the_newest_TakenAt()
    {
        var takenAt = DateTime.Now;

        var comparison = BackupComparer.Compare(SaveStatus.NoSaves(), true, [Archive(takenAt)]);

        Assert.Equal(BackupComparisonKind.NoSaveData, comparison.Kind);
        Assert.NotNull(comparison.NewestBackupTakenAt);
        Assert.Equal(takenAt, comparison.NewestBackupTakenAt);
    }

    [Fact]
    public void PathError_folds_into_NoSaveData_with_no_backups()
    {
        var comparison = BackupComparer.Compare(SaveStatus.PathError("bad template"), true, []);

        Assert.Equal(BackupComparisonKind.NoSaveData, comparison.Kind);
    }

    [Fact]
    public void PathError_folds_into_NoSaveData_and_still_carries_the_newest_TakenAt_when_backups_exist()
    {
        var takenAt = DateTime.Now;

        var comparison = BackupComparer.Compare(SaveStatus.PathError("bad template"), true, [Archive(takenAt)]);

        Assert.Equal(BackupComparisonKind.NoSaveData, comparison.Kind);
        Assert.Equal(takenAt, comparison.NewestBackupTakenAt);
    }

    [Fact]
    public void Found_with_a_null_LastModified_folds_into_NoSaveData_rather_than_throwing()
    {
        var saveStatus = SaveStatus.Found(0, 0, null, oversized: false);

        var comparison = BackupComparer.Compare(saveStatus, true, []);

        Assert.Equal(BackupComparisonKind.NoSaveData, comparison.Kind);
    }

    [Fact]
    public void Two_archives_sharing_a_TakenAt_reads_position_zero_and_never_the_last_element()
    {
        var sharedTakenAt = new DateTime(2026, 8, 1, 12, 0, 0);
        var lastModified = sharedTakenAt.AddMinutes(-10);
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);
        // Supplied newest-first, as ListBackups guarantees; a stray read of [^1] here would still
        // land on an equal TakenAt, so the second, distinct assertion below covers the real risk.
        var backups = new[] { Archive(sharedTakenAt, "b.zip"), Archive(sharedTakenAt, "a.zip") };

        var comparison = BackupComparer.Compare(saveStatus, true, backups);

        Assert.Equal(backups[0].TakenAt, comparison.NewestBackupTakenAt);
    }

    [Fact]
    public void Given_a_list_supplied_newest_first_the_result_never_reads_the_last_element()
    {
        var lastModified = new DateTime(2026, 8, 1, 12, 0, 0);
        var newest = lastModified.AddMinutes(10);
        var oldest = lastModified.AddDays(-30);
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);
        var backups = new[] { Archive(newest, "newest.zip"), Archive(oldest, "oldest.zip") };

        var comparison = BackupComparer.Compare(saveStatus, true, backups);

        Assert.Equal(newest, comparison.NewestBackupTakenAt);
        Assert.NotEqual(oldest, comparison.NewestBackupTakenAt);
    }

    [Fact]
    public void Restoring_an_older_archive_reports_SavesChangedSinceBackup_against_the_newer_safety_backup_despite_the_mtime_comparison_alone_reading_UpToDate()
    {
        // The exact misleading-verdict scenario: a restore writes a fresh pre-restore safety
        // backup (newestTakenAt, just now) before overwriting disk content with an OLDER archive
        // (restoredAt). The restored archive's own old file timestamps make lastModified <
        // newestTakenAt — the plain rule below would call that UpToDate — but the newest backup
        // actually holds the DIFFERENT, newer pre-restore content, not what's on disk now.
        var restoredAt = new DateTime(2026, 1, 1, 0, 0, 0);
        var lastModified = restoredAt; // the restored archive's own old mtime, now on disk
        var newestTakenAt = restoredAt.AddDays(300); // the fresh safety backup, taken just now
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);
        var backups = new[] { Archive(newestTakenAt) };

        var comparison = BackupComparer.Compare(saveStatus, true, backups, restoredArchiveTakenAt: restoredAt);

        Assert.Equal(BackupComparisonKind.SavesChangedSinceBackup, comparison.Kind);
        Assert.Equal(newestTakenAt, comparison.NewestBackupTakenAt);
        Assert.Equal(newestTakenAt - restoredAt, comparison.Elapsed);
    }

    [Fact]
    public void Restoring_the_newest_archive_itself_still_returns_UpToDate()
    {
        // The common case: restoring whatever is already the newest backup must not be affected
        // by the restoredArchiveTakenAt hint — disk content genuinely matches the newest backup.
        var takenAt = new DateTime(2026, 1, 1, 0, 0, 0);
        var saveStatus = SaveStatus.Found(1, 10, takenAt, oversized: false);
        var backups = new[] { Archive(takenAt) };

        var comparison = BackupComparer.Compare(saveStatus, true, backups, restoredArchiveTakenAt: takenAt);

        Assert.Equal(BackupComparisonKind.UpToDate, comparison.Kind);
    }

    [Fact]
    public void A_null_restoredArchiveTakenAt_leaves_the_plain_mtime_comparison_unaffected()
    {
        var lastModified = new DateTime(2026, 8, 1, 12, 0, 0);
        var takenAt = lastModified.AddMinutes(5);
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);

        var comparison = BackupComparer.Compare(saveStatus, true, [Archive(takenAt)], restoredArchiveTakenAt: null);

        Assert.Equal(BackupComparisonKind.UpToDate, comparison.Kind);
    }

    [Fact]
    public void Compare_called_twice_with_equal_inputs_returns_two_equal_BackupComparison_values()
    {
        var lastModified = new DateTime(2026, 8, 1, 12, 0, 0);
        var takenAt = lastModified.AddMinutes(5);
        var saveStatus = SaveStatus.Found(1, 10, lastModified, oversized: false);
        var backups = new[] { Archive(takenAt) };

        var first = BackupComparer.Compare(saveStatus, true, backups);
        var second = BackupComparer.Compare(saveStatus, true, backups);

        Assert.Equal(first, second);
    }
}
