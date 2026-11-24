using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// RestoreCommand: the busy gate, the confirmation copy, and the distinct restore errors.
/// </summary>
[Collection("MainViewModel")]
public sealed class MainViewModelRestoreTests : MainViewModelTestBase
{
    // --- RestoreCommand, the busy gate, the confirmation copy, the distinct errors ---

    [Fact]
    public async Task Restoring_with_a_backup_on_disk_and_confirmation_accepted_completes_the_restore()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = true;

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        var saveDir = Path.Combine(_dir, "saves");
        var files = Directory.GetFiles(saveDir);
        var file = Assert.Single(files);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));

        var preRestoreArchives = Directory.GetFiles(Path.Combine(backupRoot, "test-game"), "*-pre-restore.zip");
        Assert.Single(preRestoreArchives);

        Assert.Contains("Restored", vm.StatusMessage);
        Assert.Contains("Test Game", vm.StatusMessage);
        // The archive path used to be quoted back here. A success message names the game and not a
        // stored path — the goal is that nothing on screen requires knowing how SavegameBunker
        // stores its data. The path stays on the backup-folder tooltip for anyone who wants it.
        Assert.DoesNotContain(picked.FilePath, vm.StatusMessage);
    }

    [Fact]
    public async Task A_second_restore_click_on_the_same_row_before_the_first_settles_is_ignored()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = true;

        // row.IsBusy is raised synchronously before RestoreAsync's first await, so this second
        // Execute (simulating a same-row double click before the precondition check or the picker
        // dialog has returned) must be a no-op rather than starting a second restore against the
        // same save directory.
        vm.RestoreCommand.Execute(vm.Games[0]);
        vm.RestoreCommand.Execute(vm.Games[0]);
        // RestoreCommand has no CanExecute gate (it is always true), so row.IsBusy — the guard
        // under test — is the only reliable signal that the run has actually settled.
        await WaitForAsync(() => !vm.Games[0].IsBusy);

        Assert.Single(dialog.ConfirmMessages);
        Assert.Single(Directory.GetFiles(Path.Combine(backupRoot, "test-game"), "*-pre-restore.zip"));
    }

    [Fact]
    public async Task Cancelling_the_picker_writes_nothing_takes_no_safety_backup_and_reports_cancellation()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        SeedBackup(backupRoot, vm.Games[0].Game);

        dialog.RestorePickerSelectionToReturn = null;

        var beforeBytes = File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves")));

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        Assert.Equal(beforeBytes, File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves"))));
        Assert.Empty(Directory.GetFiles(Path.Combine(backupRoot, "test-game"), "*-pre-restore.zip"));
        Assert.Empty(dialog.ConfirmMessages);
        Assert.Equal("Restore cancelled.", vm.StatusMessage);
    }

    [Fact]
    public async Task The_restore_picker_is_given_a_delete_action_that_removes_exactly_that_archive()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        dialog.RestorePickerSelectionToReturn = null;

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => dialog.RestorePickerCalls.Count == 1);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        var deleteArchive = Assert.Single(dialog.RestorePickerDeleteActions);
        var result = deleteArchive(picked.FilePath);

        Assert.True(result.Success, result.Message);
        Assert.False(File.Exists(picked.FilePath));
    }

    [Fact]
    public async Task Declining_confirmation_leaves_the_save_directory_unchanged_and_removes_the_safety_archive()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = false;

        var beforeBytes = File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves")));

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        Assert.Equal(beforeBytes, File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves"))));
        Assert.Empty(Directory.GetFiles(Path.Combine(backupRoot, "test-game"), "*-pre-restore.zip"));
        Assert.Single(dialog.ConfirmMessages);
        // Declining is not a failure (SPEC: "Declining changes nothing") — only the status line
        // reports it, no error dialog.
        Assert.Empty(dialog.ErrorMessages);
        Assert.Empty(dialog.InfoMessages);
        Assert.Contains("cancelled", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Plain_confirmation_names_game_timestamp_save_dir_and_safety_backup_with_no_warning()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = true;

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        var message = Assert.Single(dialog.ConfirmMessages);
        Assert.Contains("Test Game", message);
        Assert.Contains(Path.Combine(_dir, "saves"), message);
        Assert.Contains("backed up first, just in case", message);
        Assert.DoesNotContain("Warning", message);
        Assert.DoesNotContain(" of ", message);

        Assert.Single(Directory.GetFiles(Path.Combine(backupRoot, "test-game"), "*-pre-restore.zip"));
    }

    [Fact]
    public async Task Warning_case_confirmation_includes_the_elapsed_warning_paragraph()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        // Force "saves changed since backup" deterministically rather than racing the clock.
        File.SetLastWriteTime(SaveFilePath(Path.Combine(_dir, "saves")), picked.TakenAt.AddHours(1));

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = true;

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        var message = Assert.Single(dialog.ConfirmMessages);
        Assert.Contains("Warning: the current saves are newer than this backup by 1 hour", message);
        Assert.Contains("restoring now will undo that play", message);
    }

    [Fact]
    public async Task Nothing_to_back_up_case_uses_the_extension_sentence_and_still_completes()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        File.Delete(SaveFilePath(Path.Combine(_dir, "saves")));

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = true;

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        var message = Assert.Single(dialog.ConfirmMessages);
        Assert.Contains("There were no current saves to back up first", message);
        Assert.DoesNotContain("backed up first, just in case", message);

        Assert.Contains("Restored", vm.StatusMessage);
    }

    /// <summary>
    /// Backstop: drives <c>GameActions.BuildConfirmation</c> (private, so exercised through the
    /// full restore flow like every other confirmation test here) down both the no-current-saves
    /// branch and the newer-saves-than-backup branch, and proves the two paragraphs the contract
    /// says can never contradict each other never actually co-occur in either direction.
    /// </summary>
    [Fact]
    public async Task No_current_saves_paragraph_and_newer_saves_warning_never_co_occur()
    {
        {
            var noSavesBackupRoot = Path.Combine(_dir, "backups-no-saves");
            var (vm, dialog, _) = CreateViewModel(noSavesBackupRoot);
            var picked = SeedBackup(noSavesBackupRoot, vm.Games[0].Game);
            File.Delete(SaveFilePath(Path.Combine(_dir, "saves")));
            dialog.RestorePickerSelectionToReturn = picked;
            dialog.ConfirmToReturn = true;

            vm.RestoreCommand.Execute(vm.Games[0]);
            await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

            var message = Assert.Single(dialog.ConfirmMessages);
            Assert.Contains("There were no current saves to back up", message);
            Assert.DoesNotContain("Warning: the current saves are newer than this backup", message);
        }

        {
            var warningBackupRoot = Path.Combine(_dir, "backups-warning");
            var (vm, dialog, _) = CreateViewModel(warningBackupRoot);
            var picked = SeedBackup(warningBackupRoot, vm.Games[0].Game);
            File.SetLastWriteTime(SaveFilePath(Path.Combine(_dir, "saves")), picked.TakenAt.AddHours(1));
            dialog.RestorePickerSelectionToReturn = picked;
            dialog.ConfirmToReturn = true;

            vm.RestoreCommand.Execute(vm.Games[0]);
            await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

            var message = Assert.Single(dialog.ConfirmMessages);
            Assert.Contains("Warning: the current saves are newer than this backup", message);
            Assert.DoesNotContain("There were no current saves to back up", message);
        }
    }

    [Fact]
    public async Task No_backups_for_the_game_reports_a_distinct_error_and_never_opens_the_picker()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(backupRoot);
        var (vm, dialog, _) = CreateViewModel(backupRoot);

        var beforeBytes = File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves")));

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        Assert.Empty(dialog.RestorePickerCalls);
        Assert.Equal(beforeBytes, File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves"))));
        Assert.Contains("No backups found for Test Game", vm.StatusMessage);
        Assert.Contains("Back it up before you can restore", vm.StatusMessage);
        Assert.NotEmpty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task Unreachable_backup_root_reports_a_distinct_error_naming_it_and_never_opens_the_picker()
    {
        var backupRoot = Path.Combine(_dir, "does-not-exist");
        var (vm, dialog, _) = CreateViewModel(backupRoot);

        var beforeBytes = File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves")));

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        Assert.Empty(dialog.RestorePickerCalls);
        Assert.Equal(beforeBytes, File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves"))));
        Assert.Contains(backupRoot, vm.StatusMessage);
        Assert.Contains("isn't available right now", vm.StatusMessage);
        Assert.NotEmpty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task Unlistable_backup_folder_reports_unavailable_not_no_backups()
    {
        // The folder exists and holds an archive, but this game's own subfolder can't be
        // enumerated (a permission-denied share, say). Before TryListBackups this collapsed into
        // the same "no backups" empty list as a game that was never backed up at all — sending the
        // user to make a first backup into a folder the app just proved it can't read.
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        SeedBackup(backupRoot, vm.Games[0].Game);
        var targetDir = Path.Combine(backupRoot, "test-game");

        if (!TestFileSystem.TryDenyAccess(targetDir))
            return; // Environment cannot apply the ACL — see TestFileSystem.TryDenyAccess.

        var beforeBytes = File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves")));

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        Assert.Empty(dialog.RestorePickerCalls);
        Assert.Equal(beforeBytes, File.ReadAllBytes(SaveFilePath(Path.Combine(_dir, "saves"))));
        Assert.Contains("isn't available right now", vm.StatusMessage);
        Assert.DoesNotContain("No backups found", vm.StatusMessage);
    }

    [Fact]
    public async Task Status_sweep_reports_folder_not_available_for_an_unlistable_game_folder_not_never_backed_up()
    {
        // Same bug as the restore pre-flight fix (TryListBackups), on a different surface: the
        // status sweep used to call plain ListBackups, so a game folder that exists but can't be
        // enumerated read identically to "never backed up" in the grid's own Status column.
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, _, catalogPath) = CreateViewModel(backupRoot);
        // Refresh reloads the catalog from disk; without the file it would seed an empty template
        // and drop the row this test is about.
        var saved = await GameCatalog.SaveAsync(catalogPath, new Catalog { BackupRoot = backupRoot, Games = [vm.Games[0].Game] });
        Assert.True(saved.Success, saved.Message);
        SeedBackup(backupRoot, vm.Games[0].Game);
        var targetDir = Path.Combine(backupRoot, "test-game");

        if (!TestFileSystem.TryDenyAccess(targetDir))
            return; // Environment cannot apply the ACL — see TestFileSystem.TryDenyAccess.

        vm.RefreshCommand.Execute(null);
        // The constructor's own sweep may still report its earlier verdict before Refresh flips the
        // row to Checking, so wait for the expected verdict rather than for "not Checking".
        await WaitForAsync(() => vm.Games.Count > 0 && vm.Games[0].ComparisonKind == BackupComparisonKind.BackupFolderNotAvailable);
    }

    [Fact]
    public async Task Changing_the_backup_root_re_sweeps_every_row()
    {
        var oldRoot = Path.Combine(_dir, "old-backups");
        Directory.CreateDirectory(oldRoot);
        var (vm, dialog, _) = CreateViewModel(oldRoot);
        var newRoot = Path.Combine(_dir, "new-backups");
        Directory.CreateDirectory(newRoot);
        SeedBackup(newRoot, vm.Games[0].Game);

        await WaitForAsync(() => vm.Games[0].ComparisonKind != BackupComparisonKind.Checking);
        Assert.Equal(BackupComparisonKind.NeverBackedUp, vm.Games[0].ComparisonKind);

        dialog.FolderToReturn = newRoot;
        await vm.ChangeBackupRootCommand.ExecuteAsync(null);
        await WaitForAsync(() => vm.Games[0].ComparisonKind == BackupComparisonKind.UpToDate);
    }

    [Fact]
    public async Task Restoring_an_older_archive_reports_SavesChangedSinceBackup_not_UpToDate_and_a_fresh_backup_clears_it()
    {
        // The exact misleading-verdict scenario this fix closes: restoring anything other than the
        // newest backup makes the fresh pre-restore safety backup the newest archive, whose
        // content differs from what's now on disk (the just-restored, OLDER archive's content).
        // The plain mtime rule alone would call that UpToDate since the safety backup is always
        // taken after whatever ends up on disk.
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var savePath = Path.Combine(_dir, "saves", "save.dat");

        var gameDir = Path.Combine(backupRoot, "test-game");
        Directory.CreateDirectory(gameDir);
        var olderContentDir = Path.Combine(_dir, "older-content");
        Directory.CreateDirectory(olderContentDir);
        File.WriteAllBytes(Path.Combine(olderContentDir, "save.dat"), [9, 9, 9]);
        ZipFile.CreateFromDirectory(olderContentDir, Path.Combine(gameDir, "2020-01-01_000000.zip"));
        var olderArchive = Assert.Single(new BackupService().ListBackups(backupRoot, "test-game"));

        dialog.RestorePickerSelectionToReturn = olderArchive;
        dialog.ConfirmToReturn = true;

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));
        await WaitForAsync(() => vm.Games[0].ComparisonKind != BackupComparisonKind.Checking);

        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(savePath));
        Assert.Equal(BackupComparisonKind.SavesChangedSinceBackup, vm.Games[0].ComparisonKind);

        vm.BackupCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.BackupCommand.CanExecute(vm.Games[0]));
        await WaitForAsync(() => vm.Games[0].ComparisonKind != BackupComparisonKind.Checking);

        Assert.Equal(BackupComparisonKind.UpToDate, vm.Games[0].ComparisonKind);
    }

    [Fact]
    public async Task Unset_backup_root_reports_a_distinct_error_and_never_opens_the_picker_or_folder_dialog()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "");

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));

        Assert.Empty(dialog.RestorePickerCalls);
        Assert.Equal(0, dialog.PickFolderCalls);
        Assert.Contains("no backup folder is set yet", vm.StatusMessage);
        Assert.Contains("Use Backup folder", vm.StatusMessage);
        Assert.NotEmpty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task IsRestoreRunning_gates_Refresh_and_ChangeBackupRoot_and_resets_after_completion()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = true;

        bool? refreshCanExecute = null;
        bool? changeBackupRootCanExecute = null;
        dialog.OnReport = () =>
        {
            refreshCanExecute = vm.RefreshCommand.CanExecute(null);
            changeBackupRootCanExecute = vm.ChangeBackupRootCommand.CanExecute(null);
        };

        vm.RestoreCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => refreshCanExecute is not null && changeBackupRootCanExecute is not null);

        Assert.False(refreshCanExecute);
        Assert.False(changeBackupRootCanExecute);

        await WaitForAsync(() => vm.RefreshCommand.CanExecute(null));
        Assert.True(vm.ChangeBackupRootCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartStatusScan_runs_again_after_a_successful_restore()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        // Let the construction-time sweep settle first, so the post-restore reset is
        // distinguishable from it.
        await WaitForAsync(() => vm.Games[0].StatusText != "Checking…");

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = true;

        var sawCheckingAgain = false;
        vm.Games[0].PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameRowViewModel.StatusText) && vm.Games[0].StatusText == "Checking…")
                sawCheckingAgain = true;
        };

        vm.RestoreCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));
        await WaitForAsync(() => sawCheckingAgain);
    }

    [Fact]
    public async Task A_configured_existing_backup_with_an_archive_settles_on_a_definite_verdict_with_a_populated_newest_backup_text()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
        var game = new Game { Id = "test-game", DisplayName = "Test Game", SaveDirTemplate = saveDir.Replace('\\', '/') };
        // Seeded before the view model is constructed, and never rewritten afterwards, so its
        // last-write time (set two seconds before the archive) stays older than the archive.
        SeedBackup(backupRoot, game);

        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog { BackupRoot = backupRoot, Games = [game] };
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(catalogPath, catalog, [], false, null);
        var vm = new MainViewModel(load, dialog);

        await WaitForAsync(() => vm.Games[0].VerdictText != "Checking…");

        Assert.Equal("Up to date", vm.Games[0].VerdictText);
        Assert.NotNull(vm.Games[0].NewestBackupAt);
    }

    [Fact]
    public async Task An_unset_backup_folder_settles_every_row_on_the_unavailable_verdict()
    {
        var (vm, _, _) = CreateViewModel(backupRoot: "");

        await WaitForAsync(() => vm.Games[0].VerdictText != "Checking…");

        Assert.Equal("Backup folder unavailable", vm.Games[0].VerdictText);
    }

    [Fact]
    public async Task A_configured_but_missing_backup_folder_settles_every_row_on_the_unavailable_verdict()
    {
        var backupRoot = Path.Combine(_dir, "does-not-exist");
        var (vm, _, _) = CreateViewModel(backupRoot);

        await WaitForAsync(() => vm.Games[0].VerdictText != "Checking…");

        Assert.Equal("Backup folder unavailable", vm.Games[0].VerdictText);
    }

    [Fact]
    public async Task An_existing_backup_folder_with_no_archives_for_the_game_settles_on_never_backed_up()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(backupRoot);
        var (vm, _, _) = CreateViewModel(backupRoot);

        await WaitForAsync(() => vm.Games[0].VerdictText != "Checking…");

        Assert.Equal("Never backed up", vm.Games[0].VerdictText);
        Assert.Null(vm.Games[0].NewestBackupAt);
        Assert.False(vm.Games[0].HasNewestBackup);
    }

    [Fact]
    public async Task Restoring_resets_every_rows_verdict_to_checking_before_it_settles_again()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);

        // Let the construction-time sweep settle first, so the post-restore reset is
        // distinguishable from it.
        await WaitForAsync(() => vm.Games[0].VerdictText != "Checking…");

        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = true;

        var sawCheckingAgain = false;
        vm.Games[0].PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameRowViewModel.VerdictText) && vm.Games[0].VerdictText == "Checking…")
                sawCheckingAgain = true;
        };

        vm.RestoreCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => vm.RestoreCommand.CanExecute(vm.Games[0]));
        await WaitForAsync(() => sawCheckingAgain);
    }

    [Fact]
    public async Task A_completed_backup_leaves_every_row_back_at_the_checking_text()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, _, _) = CreateViewModel(backupRoot);

        await WaitForAsync(() => vm.Games[0].VerdictText != "Checking…");

        var sawCheckingAgain = false;
        vm.Games[0].PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameRowViewModel.VerdictText) && vm.Games[0].VerdictText == "Checking…")
                sawCheckingAgain = true;
        };

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => vm.RefreshCommand.CanExecute(null));
        await WaitForAsync(() => sawCheckingAgain);
    }

    [Fact]
    public async Task An_empty_catalog_completes_construction_and_a_refresh_without_throwing()
    {
        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog { BackupRoot = Path.Combine(_dir, "backups"), Games = [] };
        await GameCatalog.SaveAsync(catalogPath, catalog);

        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);

        vm.RefreshCommand.Execute(null);
        await WaitForAsync(() => vm.RefreshCommand.CanExecute(null));

        Assert.Empty(vm.Games);
    }

    [Fact]
    public async Task Confirming_a_new_save_folder_writes_it_through_CatalogSession_and_leaves_id_and_name_untouched()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        var originalGame = vm.Games[0].Game;
        var newFolder = Path.Combine(_dir, "moved-saves");
        Directory.CreateDirectory(newFolder);
        dialog.ChangeSaveFolderToReturn = newFolder;

        vm.ChangeSaveFolderCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.ChangeSaveFolderCommand.CanExecute(vm.Games[0]));

        var updatedGame = vm.Games[0].Game;
        Assert.Equal(originalGame.Id, updatedGame.Id);
        Assert.Equal(originalGame.DisplayName, updatedGame.DisplayName);
        Assert.Equal(newFolder, updatedGame.SaveDirTemplate);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        var onDisk = Assert.Single(reloaded.Catalog.Games);
        Assert.Equal(newFolder, onDisk.SaveDirTemplate);

        Assert.Single(dialog.ChangeSaveFolderCalls);
        Assert.Contains("Save folder updated for Test Game.", dialog.InfoMessages);
    }

    [Fact]
    public async Task A_failed_save_folder_write_rolls_the_row_back_to_its_previous_template()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        var originalTemplate = vm.Games[0].Game.SaveDirTemplate;
        dialog.ChangeSaveFolderToReturn = Path.Combine(_dir, "moved-saves");

        // Holding the destination open with no shared access makes the write fail, matching
        // GameCatalogTests' own technique for forcing SaveAsync to fail.
        using (new FileStream(catalogPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.ChangeSaveFolderCommand.Execute(vm.Games[0]);
            await WaitForAsync(() => vm.ChangeSaveFolderCommand.CanExecute(vm.Games[0]));
        }

        Assert.Equal(originalTemplate, vm.Games[0].Game.SaveDirTemplate);
        Assert.NotEmpty(dialog.ErrorMessages);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        var onDisk = Assert.Single(reloaded.Catalog.Games);
        Assert.Equal(originalTemplate, onDisk.SaveDirTemplate);
    }

    [Fact]
    public async Task Cancelling_the_change_save_folder_dialog_writes_nothing_and_reports_nothing()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        var originalBytes = await File.ReadAllBytesAsync(catalogPath);
        dialog.ChangeSaveFolderToReturn = null;

        vm.ChangeSaveFolderCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.ChangeSaveFolderCommand.CanExecute(vm.Games[0]));

        Assert.Empty(dialog.InfoMessages);
        Assert.Empty(dialog.ErrorMessages);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(catalogPath));
    }

    [Fact]
    public async Task Confirming_remove_drops_the_row_and_catalog_entry_and_leaves_saves_and_backups_on_disk()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var archiveDir = Path.Combine(backupRoot, "test-game");
        Directory.CreateDirectory(archiveDir);
        var archivePath = Path.Combine(archiveDir, "2026-01-01_000000.zip");
        await File.WriteAllBytesAsync(archivePath, [1, 2, 3]);

        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync(backupRoot);
        var savePath = Path.Combine(_dir, "saves", "save.dat");
        dialog.ConfirmToReturn = true;

        vm.RemoveGameCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.HasNoGames);

        Assert.Empty(vm.Games);
        Assert.True(vm.HasNoGames);
        Assert.Contains("Removed Test Game from the list.", Assert.Single(dialog.InfoMessages));
        Assert.Contains("Saves and backups stay on disk.", Assert.Single(dialog.ConfirmMessages));

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        Assert.Empty(reloaded.Catalog.Games);
        Assert.True(File.Exists(savePath));
        Assert.True(File.Exists(archivePath));
    }

    [Fact]
    public async Task Confirming_remove_drops_only_that_entry_and_keeps_the_other_game_persisted()
    {
        // Two saved games so "removes the entry" is distinguishable from "clears the catalog":
        // the single-game variant above cannot tell the two apart.
        var catalogPath = Path.Combine(_dir, "two-games.json");
        var saveDirs = new List<string>();
        var catalog = new Catalog
        {
            Games = [],
        };
        for (var i = 0; i < 2; i++)
        {
            var saveDir = Path.Combine(_dir, $"remove-saves-{i}");
            Directory.CreateDirectory(saveDir);
            File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
            saveDirs.Add(saveDir);
            catalog.Games.Add(new Game
            {
                Id = $"remove-game-{i}",
                DisplayName = $"Remove Game {i}",
                SaveDirTemplate = saveDir.Replace('\\', '/'),
            });
        }

        await GameCatalog.SaveAsync(catalogPath, catalog);
        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);
        dialog.ConfirmToReturn = true;

        vm.RemoveGameCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.Games.Count == 1);

        Assert.Equal("remove-game-1", vm.Games[0].Game.Id);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        var entry = Assert.Single(reloaded.Catalog.Games);
        Assert.Equal("remove-game-1", entry.Id);
        Assert.True(File.Exists(Path.Combine(saveDirs[0], "save.dat")));
        Assert.True(File.Exists(Path.Combine(saveDirs[1], "save.dat")));
    }

    [Fact]
    public async Task Declining_remove_writes_nothing_and_leaves_the_row()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        var originalBytes = await File.ReadAllBytesAsync(catalogPath);
        dialog.ConfirmToReturn = false;

        vm.RemoveGameCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.RemoveGameCommand.CanExecute(vm.Games[0]));

        Assert.Single(vm.Games);
        Assert.Empty(dialog.InfoMessages);
        Assert.Empty(dialog.ErrorMessages);
        Assert.Single(dialog.ConfirmMessages);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(catalogPath));
    }

    [Fact]
    public async Task A_failed_remove_write_leaves_the_row_and_the_catalog_file()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        dialog.ConfirmToReturn = true;

        using (new FileStream(catalogPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.RemoveGameCommand.Execute(vm.Games[0]);
            await WaitForAsync(() => vm.RemoveGameCommand.CanExecute(vm.Games[0]));
        }

        Assert.Single(vm.Games);
        Assert.Equal("test-game", vm.Games[0].Game.Id);
        Assert.NotEmpty(dialog.ErrorMessages);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        Assert.Single(reloaded.Catalog.Games);
    }

    [Fact]
    public async Task Removing_a_selected_game_clears_it_from_the_selection()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        vm.Games[0].IsSelected = true;
        dialog.ConfirmToReturn = true;

        vm.RemoveGameCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.HasNoGames);

        Assert.False(vm.HasAnySelected);
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public async Task Opening_the_save_folder_for_a_row_with_an_unresolvable_template_reports_the_game_cells_own_sentence()
    {
        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog
        {
            BackupRoot = Path.Combine(_dir, "backups"),
            Games = [new() { Id = "broken", DisplayName = "Broken Game", SaveDirTemplate = "%NOT_A_REAL_VARIABLE%/Saves" }],
        };
        await GameCatalog.SaveAsync(catalogPath, catalog);
        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);

        vm.OpenSaveFolderCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.OpenSaveFolderCommand.CanExecute(vm.Games[0]));

        Assert.Contains("Can't find this game's save folder.", dialog.ErrorMessages);
    }

    [Fact]
    public async Task Bulk_backup_selects_exactly_the_changed_and_never_backed_up_rows()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(6);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[1].ApplyBackupComparison(BackupComparison.SavesChangedSinceBackup(DateTime.Now.AddDays(-1), TimeSpan.FromDays(1)));
        vm.Games[2].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[3].ApplyBackupComparison(BackupComparison.NoSaveData(DateTime.Now));
        vm.Games[4].ApplyBackupComparison(BackupComparison.FolderNotAvailable());
        vm.Games[5].ApplyBackupComparison(BackupComparison.NeverBackedUp());

        Assert.True(vm.HasBackupTargets);
        Assert.True(vm.BackupAllChangedCommand.CanExecute(null));

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null));

        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-0")));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-1")));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-2")));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-3")));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-4")));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-5")));
    }

    [Fact]
    public async Task Bulk_backup_cannot_execute_while_any_row_is_still_checking()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[1].ApplyBackupComparison(BackupComparison.Checking());

        Assert.False(vm.BackupAllChangedCommand.CanExecute(null));

        vm.Games[1].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));

        Assert.True(vm.BackupAllChangedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Bulk_backup_cannot_execute_while_a_single_row_backup_is_running()
    {
        // CanBackupAllChanged used to gate only on IsAnyRowChecking, so a bulk run could start on
        // top of a single-row Backup already writing the same backupRoot — two writers, one folder,
        // no busy flag covering the bulk button. Observed via OnReport, which fires after the
        // backup returns but before BackupAsync's finally clears IsBackupRunning.
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[1].ApplyBackupComparison(BackupComparison.NeverBackedUp());

        Assert.True(vm.BackupAllChangedCommand.CanExecute(null));

        bool? bulkCanExecute = null;
        dialog.OnReport = () => bulkCanExecute = vm.BackupAllChangedCommand.CanExecute(null);

        vm.BackupCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => bulkCanExecute is not null);
        await WaitForAsync(() => !vm.IsBackupRunning);

        Assert.False(bulkCanExecute);
    }

    [Fact]
    public async Task Bulk_backup_cannot_execute_while_a_row_is_busy_in_restore_preflight()
    {
        // row.IsBusy is set for the whole restore window, including preflight (precondition
        // check, folder picker) — before IsRestoreRunning is raised (see RestoreAsync's own doc
        // comment). CanBackupAllChanged used to gate only on IsRestoreRunning, so a bulk run could
        // start on top of a restore still picking its archive.
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[1].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        Assert.True(vm.BackupAllChangedCommand.CanExecute(null));

        vm.Games[0].IsBusy = true;

        Assert.False(vm.BackupAllChangedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Refresh_and_change_backup_root_cannot_execute_while_a_row_is_busy_in_restore_preflight()
    {
        // Same preflight window as the bulk-backup case above: refreshing would clear Games out
        // from under the row a pending restore preflight still holds a reference to, and changing
        // the backup folder mid-preflight would point a since-started safety backup somewhere the
        // picker no longer agrees with.
        var (vm, _, _) = await CreateMultiGameViewModelAsync(1);
        Assert.True(vm.RefreshCommand.CanExecute(null));
        Assert.True(vm.ChangeBackupRootCommand.CanExecute(null));

        vm.Games[0].IsBusy = true;

        Assert.False(vm.RefreshCommand.CanExecute(null));
        Assert.False(vm.ChangeBackupRootCommand.CanExecute(null));
    }

    [Fact]
    public async Task Setting_row_IsBusy_raises_CanExecuteChanged_on_the_toolbar_commands()
    {
        // Setting row.IsBusy alone changes what IsAnyRowBusy reads, but that read is a plain
        // computed property — nothing raises PropertyChanged for it on its own. Without
        // RegisterRow's subscription forwarding the row's own IsBusy change into
        // NotifyCanExecuteChanged, the toolbar buttons would only catch up on some unrelated WPF
        // command requery instead of the moment preflight actually starts.
        var (vm, _, _) = await CreateMultiGameViewModelAsync(1);

        var raised = new List<string>();
        vm.BackupAllChangedCommand.CanExecuteChanged += (_, _) => raised.Add(nameof(vm.BackupAllChangedCommand));
        vm.RefreshCommand.CanExecuteChanged += (_, _) => raised.Add(nameof(vm.RefreshCommand));
        vm.ChangeBackupRootCommand.CanExecuteChanged += (_, _) => raised.Add(nameof(vm.ChangeBackupRootCommand));

        vm.Games[0].IsBusy = true;

        Assert.Contains(nameof(vm.BackupAllChangedCommand), raised);
        Assert.Contains(nameof(vm.RefreshCommand), raised);
        Assert.Contains(nameof(vm.ChangeBackupRootCommand), raised);
    }

    [Fact]
    public async Task Setting_row_IsBusy_raises_CanExecuteChanged_on_the_row_Backup_and_Restore_commands()
    {
        // CanBackupRow/CanRestoreRow both read row.IsBusy, but nothing forwarded a row's own
        // IsBusy change into these two commands' NotifyCanExecuteChanged — they only caught up
        // on some unrelated WPF command requery instead of the moment preflight actually starts.
        var (vm, _, _) = await CreateMultiGameViewModelAsync(1);

        var raised = new List<string>();
        vm.BackupCommand.CanExecuteChanged += (_, _) => raised.Add(nameof(vm.BackupCommand));
        vm.RestoreCommand.CanExecuteChanged += (_, _) => raised.Add(nameof(vm.RestoreCommand));

        vm.Games[0].IsBusy = true;

        Assert.Contains(nameof(vm.BackupCommand), raised);
        Assert.Contains(nameof(vm.RestoreCommand), raised);
    }

    [Fact]
    public async Task Bulk_backup_excludes_a_row_already_busy_from_its_own_target_list()
    {
        // Even if CanExecute's IsAnyRowBusy gate is stale (the target list is built after the
        // command starts), a row that went busy in the meantime must not be re-claimed.
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[1].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[0].IsBusy = true;

        await vm.BackupAllChangedCommand.ExecuteAsync(null);

        Assert.False(Directory.Exists(Path.Combine(backupRoot, vm.Games[0].Game.Id)));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, vm.Games[1].Game.Id)));
    }

    [Fact]
    public async Task With_no_qualifying_row_the_bulk_action_is_hidden_not_just_disabled()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);

        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[1].ApplyBackupComparison(BackupComparison.NoSaveData(null));

        Assert.False(vm.HasBackupTargets);
    }

    [Fact]
    public async Task A_failing_game_does_not_stop_the_run_and_the_summary_names_it()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        // Game 0's save directory is deleted after construction so BackupService.Backup fails for
        // it specifically, while game 1's real directory (from the fixture) still succeeds.
        Directory.Delete(Path.Combine(_dir, "saves-0"), recursive: true);
        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[1].ApplyBackupComparison(BackupComparison.NeverBackedUp());

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null));

        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-1")));
        var message = Assert.Single(dialog.ErrorMessages);
        Assert.Contains("Backed up 1 of 2.", message);
        Assert.Contains("Game 0", message);
    }

    [Fact]
    public async Task A_run_of_exactly_one_qualifying_game_reports_the_single_game_sentence()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[1].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null));

        Assert.Contains("Backed up Game 0.", dialog.InfoMessages);
    }

    [Fact]
    public async Task A_run_of_more_than_one_qualifying_game_reports_the_all_n_sentence()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[1].ApplyBackupComparison(BackupComparison.NeverBackedUp());

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null));

        Assert.Contains("Backed up all 2 games.", dialog.InfoMessages);
    }
}
