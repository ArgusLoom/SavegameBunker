using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Exercises <see cref="GameRowViewModel.ApplyBackupComparison"/> entirely from synthetic
/// <see cref="BackupComparison"/> values — no filesystem, matching
/// <see cref="ScanWindowViewModelTests"/>'s no-WPF view-model test shape.
/// </summary>
public sealed class GameRowViewModelTests
{
    private static GameRowViewModel MakeRow() =>
        new(new Game { Id = "test-game", DisplayName = "Test Game", SaveDirTemplate = "%APPDATA%/TestGame" });

    [Fact]
    public void Checking_shows_the_checking_text_and_no_newest_backup_with_no_colour()
    {
        var row = MakeRow();

        row.ApplyBackupComparison(BackupComparison.Checking());

        Assert.Equal("Checking…", row.VerdictText);
        Assert.Null(row.NewestBackupAt);
        Assert.False(row.HasNewestBackup);
        Assert.Equal(RowSeverity.Normal, row.StatusSeverity);
    }

    [Fact]
    public void Up_to_date_shows_the_up_to_date_text_and_the_archive_timestamp_with_no_colour()
    {
        var row = MakeRow();
        var takenAt = new DateTime(2026, 8, 30, 14, 5, 0);

        row.ApplyBackupComparison(BackupComparison.UpToDate(takenAt));

        Assert.Equal("Up to date", row.VerdictText);
        Assert.Equal(takenAt, row.NewestBackupAt);
        Assert.True(row.HasNewestBackup);
        Assert.True(row.IsUpToDate);
        Assert.Equal(BackupComparisonKind.UpToDate, row.ComparisonKind);
        Assert.Equal(RowSeverity.Normal, row.StatusSeverity);
    }

    [Fact]
    public void Saves_changed_since_backup_shows_the_elapsed_phrase_and_warning_colour()
    {
        var row = MakeRow();
        var takenAt = new DateTime(2026, 8, 28, 9, 0, 0);
        var elapsed = TimeSpan.FromHours(3);

        row.ApplyBackupComparison(BackupComparison.SavesChangedSinceBackup(takenAt, elapsed));

        Assert.Contains(ElapsedFormat.Describe(elapsed), row.VerdictText);
        Assert.Equal(takenAt, row.NewestBackupAt);
        Assert.True(row.HasNewestBackup);
        Assert.False(row.IsUpToDate);
        Assert.Equal(RowSeverity.Warning, row.StatusSeverity);
    }

    [Fact]
    public void Never_backed_up_shows_the_never_backed_up_text_with_no_newest_backup_as_a_warning()
    {
        var row = MakeRow();

        row.ApplyBackupComparison(BackupComparison.NeverBackedUp());

        Assert.Equal("Never backed up", row.VerdictText);
        Assert.Null(row.NewestBackupAt);
        Assert.False(row.HasNewestBackup);
        Assert.Equal(RowSeverity.Warning, row.StatusSeverity);
    }

    [Fact]
    public void No_save_data_with_no_backup_shows_the_nothing_backed_up_text()
    {
        var row = MakeRow();

        row.ApplyBackupComparison(BackupComparison.NoSaveData(null));

        Assert.Equal("No save data yet", row.VerdictText);
        Assert.Null(row.NewestBackupAt);
        Assert.False(row.HasNewestBackup);
    }

    [Fact]
    public void No_save_data_with_a_backup_shows_the_restore_available_text_and_the_backup_timestamp()
    {
        var row = MakeRow();
        var takenAt = new DateTime(2026, 8, 25, 18, 30, 0);

        row.ApplyBackupComparison(BackupComparison.NoSaveData(takenAt));

        Assert.Equal("No save data right now — you can still restore", row.VerdictText);
        Assert.Equal(takenAt, row.NewestBackupAt);
        Assert.True(row.HasNewestBackup);
    }

    [Fact]
    public void Backup_folder_not_available_shows_the_unavailable_text_with_no_newest_backup_and_error_colour()
    {
        var row = MakeRow();

        row.ApplyBackupComparison(BackupComparison.FolderNotAvailable());

        Assert.Equal("Backup folder unavailable", row.VerdictText);
        Assert.Null(row.NewestBackupAt);
        Assert.False(row.HasNewestBackup);
        Assert.Equal(RowSeverity.Error, row.StatusSeverity);
    }

    [Fact]
    public void Applying_a_second_comparison_replaces_the_first_cleanly()
    {
        var row = MakeRow();
        row.ApplyBackupComparison(BackupComparison.SavesChangedSinceBackup(DateTime.Now.AddDays(-1), TimeSpan.FromDays(1)));
        Assert.Equal(RowSeverity.Warning, row.StatusSeverity);

        row.ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));

        Assert.Equal(RowSeverity.Normal, row.StatusSeverity);
        Assert.Equal("Up to date", row.VerdictText);
    }

    [Fact]
    public void Each_new_property_raises_PropertyChanged_when_its_value_changes()
    {
        var row = MakeRow();
        // Start from FolderNotAvailable so both properties below have to move away from that
        // state, and every move is observable.
        row.ApplyBackupComparison(BackupComparison.FolderNotAvailable());

        var changed = new List<string>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        row.ApplyBackupComparison(BackupComparison.SavesChangedSinceBackup(DateTime.Now.AddDays(-1), TimeSpan.FromDays(1)));

        Assert.Contains(nameof(GameRowViewModel.NewestBackupAt), changed);
        Assert.Contains(nameof(GameRowViewModel.HasNewestBackup), changed);
        Assert.Contains(nameof(GameRowViewModel.VerdictText), changed);
        Assert.Contains(nameof(GameRowViewModel.StatusSeverity), changed);
    }

    [Fact]
    public void ApplyStatus_path_error_reports_the_cant_find_sentence_with_error_colour()
    {
        var row = MakeRow();

        row.ApplyStatus(SaveStatus.PathError("boom"));

        Assert.Equal("Can't find this game's save folder.", row.StatusText);
        Assert.Equal(RowSeverity.Error, row.GameSeverity);
        Assert.Null(row.LastPlayedAt);
    }

    [Fact]
    public void ApplyStatus_oversized_found_reports_the_unusually_large_sentence_with_warning_colour()
    {
        var row = MakeRow();
        var lastModified = new DateTime(2026, 8, 29, 10, 0, 0);

        row.ApplyStatus(SaveStatus.Found(5, 1024, lastModified, oversized: true));

        Assert.Equal("This folder is unusually large — make sure it's the right one", row.StatusText);
        Assert.Equal(RowSeverity.Warning, row.GameSeverity);
        Assert.Equal(lastModified, row.LastPlayedAt);
    }

    [Fact]
    public void ApplyStatus_found_not_oversized_leaves_the_sentence_blank_with_no_colour()
    {
        var row = MakeRow();
        var lastModified = new DateTime(2026, 8, 29, 10, 0, 0);

        row.ApplyStatus(SaveStatus.Found(5, 1024, lastModified, oversized: false));

        Assert.Equal("", row.StatusText);
        Assert.Equal(RowSeverity.Normal, row.GameSeverity);
        Assert.Equal(lastModified, row.LastPlayedAt);
    }

    [Fact]
    public void ApplyStatus_found_with_skipped_paths_names_them_with_warning_colour()
    {
        var row = MakeRow();
        var lastModified = new DateTime(2026, 8, 29, 10, 0, 0);
        var status = SaveStatus.Found(5, 1024, lastModified, oversized: false) with { SkippedPaths = ["locked"] };

        row.ApplyStatus(status);

        Assert.Equal(Strings.Format("str_game_unreadablePaths", 1, "locked"), row.StatusText);
        Assert.Equal(RowSeverity.Warning, row.GameSeverity);
    }

    [Fact]
    public void ApplyStatus_no_saves_with_skipped_paths_names_them_instead_of_no_saves_yet()
    {
        var row = MakeRow();
        var status = SaveStatus.NoSaves() with { SkippedPaths = ["."] };

        row.ApplyStatus(status);

        Assert.Equal(Strings.Format("str_game_unreadablePaths", 1, "."), row.StatusText);
        Assert.Equal(RowSeverity.Warning, row.GameSeverity);
        Assert.Equal("0", row.FileCountText);
    }

    [Fact]
    public void ApplyStatus_found_oversized_with_skipped_paths_shows_the_skipped_sentence()
    {
        var row = MakeRow();
        var lastModified = new DateTime(2026, 8, 29, 10, 0, 0);
        var status = SaveStatus.Found(5, 1024, lastModified, oversized: true) with { SkippedPaths = ["locked"] };

        row.ApplyStatus(status);

        Assert.Equal(Strings.Format("str_game_unreadablePaths", 1, "locked"), row.StatusText);
        Assert.Equal(RowSeverity.Warning, row.GameSeverity);
    }

    [Fact]
    public void ApplyStatus_skipped_paths_sentence_names_five_and_counts_the_rest()
    {
        var row = MakeRow();
        var seven = new[] { "a", "b", "c", "d", "e", "f", "g" };
        var status = SaveStatus.Found(1, 10, null, oversized: false) with { SkippedPaths = seven };

        row.ApplyStatus(status);

        Assert.Equal(Strings.Format("str_game_unreadablePaths", 7, "a, b, c, d, e, and 2 more"), row.StatusText);
    }

    [Fact]
    public void ApplyStatus_skipped_paths_sentence_keeps_non_ascii_names_intact()
    {
        var row = MakeRow();
        var status = SaveStatus.Found(1, 10, null, oversized: false) with { SkippedPaths = ["Ōkami/セーブ"] };

        row.ApplyStatus(status);

        Assert.Equal(Strings.Format("str_game_unreadablePaths", 1, "Ōkami/セーブ"), row.StatusText);
    }

    [Fact]
    public void IsSelected_defaults_to_false_and_round_trips_through_both_values()
    {
        var row = MakeRow();

        Assert.False(row.IsSelected);

        row.IsSelected = true;
        Assert.True(row.IsSelected);

        row.IsSelected = false;
        Assert.False(row.IsSelected);
    }

    [Fact]
    public void IsSelected_raises_PropertyChanged_when_it_changes()
    {
        var row = MakeRow();

        var changed = new List<string>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        row.IsSelected = true;

        Assert.Contains(nameof(GameRowViewModel.IsSelected), changed);
    }

    // --- Per-row byte progress state, the 50 MB threshold -----------------------------

    [Fact]
    public void A_row_at_exactly_fifty_megabytes_reports_a_large_backup()
    {
        var row = MakeRow();

        row.BackupBytesTotal = 50L * 1024 * 1024;

        Assert.True(row.IsLargeBackup);
    }

    [Fact]
    public void A_row_one_byte_below_fifty_megabytes_does_not_report_a_large_backup()
    {
        var row = MakeRow();

        row.BackupBytesTotal = 50L * 1024 * 1024 - 1;

        Assert.False(row.IsLargeBackup);
    }

    [Fact]
    public void A_row_at_the_idle_zero_total_does_not_report_a_large_backup()
    {
        var row = MakeRow();

        Assert.Equal(0, row.BackupBytesTotal);
        Assert.False(row.IsLargeBackup);
    }

    [Fact]
    public void Setting_BackupBytesTotal_raises_PropertyChanged_for_IsLargeBackup()
    {
        var row = MakeRow();

        var changed = new List<string>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        row.BackupBytesTotal = 50L * 1024 * 1024;

        Assert.Contains(nameof(GameRowViewModel.IsLargeBackup), changed);
    }
}
