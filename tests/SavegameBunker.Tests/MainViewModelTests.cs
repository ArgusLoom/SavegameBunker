using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// Drives <see cref="MainViewModel"/> end to end without a running window, through a recording
/// dialog service and real temp folders: command gating while work runs, the first-backup folder
/// prompt, Find Games, single-row and bulk backup and restore, the status sweep and its verdicts,
/// change save folder and remove, selection, search, row ordering, the free-space advisory and
/// <see cref="MainViewModel.ReportLoad"/>.
/// </summary>
[Collection("MainViewModel")]
public sealed class MainViewModelTests : MainViewModelTestBase
{
    [Fact]
    public void NotifyCloseBlockedWhileBusy_shows_an_info_message_explaining_the_refusal()
    {
        var (vm, dialog, _) = CreateViewModel(Path.Combine(_dir, "backups"));

        // The window's Closing handler calls this when it cancels a close because a backup,
        // restore or bulk backup is in flight — without a message the refusal reads as a hang.
        vm.NotifyCloseBlockedWhileBusy();

        var message = Assert.Single(dialog.InfoMessages);
        Assert.Contains("backup", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("restore", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_and_change_backup_folder_are_disabled_while_a_backup_runs()
    {
        var (vm, dialog, _) = CreateViewModel(Path.Combine(_dir, "backups"));

        bool? refreshCanExecute = null;
        bool? changeBackupRootCanExecute = null;
        dialog.OnReport = () =>
        {
            refreshCanExecute = vm.RefreshCommand.CanExecute(null);
            changeBackupRootCanExecute = vm.ChangeBackupRootCommand.CanExecute(null);
        };

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => refreshCanExecute is not null && changeBackupRootCanExecute is not null);

        Assert.False(refreshCanExecute);
        Assert.False(changeBackupRootCanExecute);
    }

    [Fact]
    public async Task Add_game_and_open_catalog_file_stay_enabled_while_a_backup_runs()
    {
        var (vm, dialog, _) = CreateViewModel(Path.Combine(_dir, "backups"));

        bool? addGameCanExecute = null;
        bool? openCatalogCanExecute = null;
        dialog.OnReport = () =>
        {
            addGameCanExecute = vm.AddGameCommand.CanExecute(null);
            openCatalogCanExecute = vm.OpenCatalogCommand.CanExecute(null);
        };

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => addGameCanExecute is not null && openCatalogCanExecute is not null);

        Assert.True(addGameCanExecute);
        Assert.True(openCatalogCanExecute);
    }

    [Fact]
    public async Task Every_command_is_enabled_again_once_the_backup_finishes()
    {
        var (vm, dialog, _) = CreateViewModel(Path.Combine(_dir, "backups"));

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => vm.RefreshCommand.CanExecute(null));

        Assert.True(vm.ChangeBackupRootCommand.CanExecute(null));
        var created = Assert.Single(dialog.InfoMessages);
        Assert.Contains("Backed up", created);
    }

    [Fact]
    public void BackupRootDisplay_shows_only_the_last_folder_segment_of_an_ordinary_path()
    {
        var (vm, _, _) = CreateViewModel(Path.Combine(_dir, "backups"));

        Assert.Equal(Strings.Format("str_header_backupFolder", "backups"), vm.BackupRootDisplay);
    }

    [Fact]
    public void BackupRootDisplay_falls_back_to_the_full_path_for_a_drive_root()
    {
        // A bare drive root ("D:\") is a valid backup destination under this project's
        // volume-agnostic rule, but Path.GetFileName returns "" for it — the display must
        // not go blank.
        var (vm, _, _) = CreateViewModel(@"D:\");

        Assert.Equal(Strings.Format("str_header_backupFolder", "D:"), vm.BackupRootDisplay);
    }

    [Fact]
    public void BackupRootDisplay_falls_back_to_the_full_path_for_a_UNC_share_root()
    {
        // A UNC share root ("\\server\share") likewise has no last segment for
        // Path.GetFileName to take.
        var (vm, _, _) = CreateViewModel(@"\\server\share");

        Assert.Equal(Strings.Format("str_header_backupFolder", @"\\server\share"), vm.BackupRootDisplay);
    }

    [Fact]
    public void BackupRootDisplay_reports_unset_when_no_backup_root_is_configured()
    {
        var (vm, _, _) = CreateViewModel(backupRoot: "");

        Assert.Equal(Strings.Get("str_header_backupFolderUnset"), vm.BackupRootDisplay);
    }

    [Fact]
    public async Task A_cancelled_first_backup_folder_prompt_writes_nothing()
    {
        var (vm, dialog, catalogPath) = CreateViewModel(backupRoot: "");
        dialog.FolderToReturn = null;

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => dialog.PickFolderCalls == 1);

        Assert.Empty(Directory.GetFiles(_dir, "*.zip", SearchOption.AllDirectories));
        Assert.False(File.Exists(catalogPath));
        Assert.Empty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task The_first_backup_folder_prompt_opens_on_the_detected_start_folder()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "");
        dialog.FolderToReturn = null;

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => dialog.PickFolderCalls == 1);

        Assert.Equal(_suggestedStartFolder, Assert.Single(dialog.PickFolderInitialDirectories));
    }

    [Fact]
    public async Task A_cancelled_first_backup_folder_prompt_reports_a_visible_message()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "");
        dialog.FolderToReturn = null;

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => dialog.PickFolderCalls == 1);

        Assert.StartsWith("Backup cancelled", vm.StatusMessage);
        Assert.Contains("no backup folder chosen", vm.StatusMessage);
        Assert.Empty(dialog.ErrorMessages);
        Assert.Empty(dialog.InfoMessages);
    }

    /// <summary>
    /// The bulk path (<see cref="MainViewModel.BackupSelectedCommand"/>) with an unset backup
    /// root opens the same picker as the row path, and cancelling it writes nothing — the
    /// reachable counterpart to the row-path cancel fact above, because
    /// <see cref="MainViewModel.BackupCommand"/> is the only route to that one. Plan 02-06 builds
    /// item 2's guard fact on this arrangement.
    /// </summary>
    [Fact]
    public async Task Back_up_selected_with_an_unset_backup_root_opens_the_picker_and_cancelling_writes_nothing()
    {
        var (vm, dialog, catalogPath) = CreateViewModel(backupRoot: "");
        dialog.FolderToReturn = null; // the user cancels the picker

        await WaitForAsync(() => vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));

        vm.Games[0].IsSelected = true;
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));
        vm.BackupSelectedCommand.Execute(null);

        await WaitForAsync(() => dialog.PickFolderCalls == 1 && vm.StatusMessage.StartsWith("Backup cancelled"));

        Assert.Equal(_suggestedStartFolder, Assert.Single(dialog.PickFolderInitialDirectories));
        Assert.Empty(Directory.GetFiles(_dir, "*.zip", SearchOption.AllDirectories));
        Assert.False(File.Exists(catalogPath));
        Assert.Empty(dialog.ErrorMessages);
        Assert.False(vm.IsBulkBackupRunning);
        Assert.False(vm.IsAnyOperationRunning);
    }

    /// <summary>
    /// While "Back up selected" is still resolving its backup folder — the start-folder probe, the
    /// picker, the catalog write — everything else that writes must be blocked: at the moment the
    /// picker is called, a row's own Backup/Restore, Refresh, Change backup folder, Back up all
    /// changed and Restore selected must all be unable to execute, and the window could not close.
    /// A row backup running beside the bulk loop could otherwise delete the other's in-flight
    /// staging file (observed from the OnPickFolder hook, with no timing assumption).
    /// </summary>
    [Fact]
    public async Task Back_up_selected_blocks_every_other_operation_while_its_folder_picker_is_open()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "");
        dialog.FolderToReturn = null; // the user cancels the picker

        await WaitForAsync(() => vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));

        vm.Games[0].IsSelected = true;
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        bool anyRunning = false, backupRow = false, restoreRow = false, refresh = false,
            changeRoot = false, backupAll = false, restoreSelected = false;
        dialog.OnPickFolder = () =>
        {
            anyRunning = vm.IsAnyOperationRunning;
            backupRow = vm.BackupCommand.CanExecute(vm.Games[0]);
            restoreRow = vm.RestoreCommand.CanExecute(vm.Games[0]);
            refresh = vm.RefreshCommand.CanExecute(null);
            changeRoot = vm.ChangeBackupRootCommand.CanExecute(null);
            backupAll = vm.BackupAllChangedCommand.CanExecute(null);
            restoreSelected = vm.RestoreSelectedCommand.CanExecute(null);
        };

        vm.BackupSelectedCommand.Execute(null);

        await WaitForAsync(() => vm.StatusMessage.StartsWith("Backup cancelled") && !vm.IsAnyOperationRunning);

        // Every gate observed at the picker moment.
        Assert.True(anyRunning);
        Assert.False(backupRow);
        Assert.False(restoreRow);
        Assert.False(refresh);
        Assert.False(changeRoot);
        Assert.False(backupAll);
        Assert.False(restoreSelected);

        // After the cancel released the guard, the row and Refresh actions are available again.
        Assert.True(vm.BackupCommand.CanExecute(vm.Games[0]));
        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    /// <summary>
    /// The bulk run captures its targets before it resolves the backup folder, and no row is busy
    /// yet in that window. Removing a game or replacing its save folder there would let the run
    /// back up a removed game, or a replaced row's old save path. The overflow menu calls Execute
    /// directly (no CanExecute), so both commands must refuse on their own while preparing.
    /// </summary>
    [Fact]
    public async Task Catalog_edits_do_nothing_while_a_bulk_run_is_preparing()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "");
        dialog.FolderToReturn = null; // the user cancels the picker
        dialog.ConfirmToReturn = true;
        dialog.ChangeSaveFolderToReturn = "C:/elsewhere";

        await WaitForAsync(() => vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));

        var row = vm.Games[0];
        row.IsSelected = true;
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        bool addGame = true, findGames = true;
        dialog.OnPickFolder = () =>
        {
            vm.RemoveGameCommand.Execute(row);
            vm.ChangeSaveFolderCommand.Execute(row);
            addGame = vm.AddGameCommand.CanExecute(null);
            findGames = vm.FindGamesCommand.CanExecute(null);
        };

        vm.BackupSelectedCommand.Execute(null);

        await WaitForAsync(() => vm.StatusMessage.StartsWith("Backup cancelled") && !vm.IsAnyOperationRunning);

        Assert.Contains(row, vm.Games);
        Assert.Empty(dialog.ConfirmMessages);
        Assert.Empty(dialog.ChangeSaveFolderCalls);
        // Both write the catalog, which the bulk run may be writing too.
        Assert.False(addGame);
        Assert.False(findGames);
        Assert.True(vm.AddGameCommand.CanExecute(null));
    }

    /// <summary>
    /// A single-row Backup choosing its first backup folder writes the catalog too, so catalog
    /// edits wait while its picker is open — the same race the bulk preparation gate closes.
    /// </summary>
    [Fact]
    public async Task Catalog_edits_wait_while_a_row_backup_chooses_its_first_backup_folder()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "");
        dialog.FolderToReturn = null; // the user cancels the picker
        await WaitForAsync(() => vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));

        bool addGame = true, findGames = true;
        dialog.OnPickFolder = () =>
        {
            addGame = vm.AddGameCommand.CanExecute(null);
            findGames = vm.FindGamesCommand.CanExecute(null);
        };

        vm.BackupCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => dialog.PickFolderCalls == 1 && !vm.IsAnyOperationRunning);

        Assert.False(addGame);
        Assert.False(findGames);
        Assert.True(vm.AddGameCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_whitespace_backup_root_opens_the_picker_like_an_absent_one()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "   ");
        dialog.FolderToReturn = null;

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => dialog.PickFolderCalls == 1);
    }

    [Fact]
    public async Task A_backup_folder_with_non_ascii_characters_round_trips_through_the_catalog()
    {
        var (vm, dialog, catalogPath) = CreateViewModel(backupRoot: "");
        var picked = Path.Combine(_dir, "Sicherungen für Spiele 日本");
        dialog.FolderToReturn = picked;

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => dialog.InfoMessages.Count == 1);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        Assert.Equal(picked.Replace('\\', '/'), reloaded.Catalog.BackupRoot);

        var zips = Directory.GetFiles(Path.Combine(picked, "test-game"), "*.zip");
        Assert.Single(zips);
    }

    [Fact]
    public async Task A_chosen_backup_folder_is_honoured_exactly_as_picked_whatever_detection_returned()
    {
        var (vm, dialog, catalogPath) = CreateViewModel(backupRoot: "");
        var picked = Path.Combine(_dir, "chosen-backups");
        dialog.FolderToReturn = picked;

        vm.BackupCommand.Execute(vm.Games[0]);

        // Real zip write + verify to disk; the default 3s budget was observed flaky under
        // full-suite parallel load (passes in isolation), the same real-I/O contention the bulk
        // backup/restore tests below already budget 15s for.
        await WaitForAsync(() => dialog.InfoMessages.Count == 1, timeoutMs: RealIoTimeoutMs);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        Assert.Equal(picked.Replace('\\', '/'), reloaded.Catalog.BackupRoot);

        var zips = Directory.GetFiles(Path.Combine(picked, "test-game"), "*.zip");
        Assert.Single(zips);

        Assert.Equal(_suggestedStartFolder, Assert.Single(dialog.PickFolderInitialDirectories));
        Assert.NotEqual(_suggestedStartFolder.Replace('\\', '/'), reloaded.Catalog.BackupRoot);
    }

    [Fact]
    public async Task Two_selected_games_neither_colliding_are_both_added()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = "%APPDATA%/A" },
            new() { Id = "game-b", DisplayName = "Game B", SaveDirTemplate = "%APPDATA%/B" },
        };

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));

        Assert.Contains(vm.Games, row => row.Game.Id == "game-a");
        Assert.Contains(vm.Games, row => row.Game.Id == "game-b");
        Assert.Contains("2", vm.StatusMessage);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        Assert.Equal(3, reloaded.Catalog.Games.Count);
    }

    [Fact]
    public async Task A_colliding_id_is_refused_by_name_while_the_rest_of_the_selection_is_still_added()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "test-game", DisplayName = "Colliding Game", SaveDirTemplate = "%APPDATA%/Collide" },
            new() { Id = "game-b", DisplayName = "Game B", SaveDirTemplate = "%APPDATA%/B" },
        };

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));

        Assert.Contains(vm.Games, row => row.Game.Id == "game-b");
        Assert.DoesNotContain(vm.Games, row => row.DisplayName == "Colliding Game");
        Assert.Contains("Colliding Game", vm.StatusMessage);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        Assert.Equal(2, reloaded.Catalog.Games.Count);
        Assert.DoesNotContain(reloaded.Catalog.Games, g => g.DisplayName == "Colliding Game");
    }

    [Fact]
    public async Task Id_collision_check_is_case_insensitive()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "Test-Game", DisplayName = "Different Case", SaveDirTemplate = "%APPDATA%/X" },
        };

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));

        Assert.DoesNotContain(vm.Games, row => row.DisplayName == "Different Case");

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        Assert.Single(reloaded.Catalog.Games);
    }

    [Fact]
    public async Task The_matched_existing_entry_is_left_unchanged()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "test-game", DisplayName = "Renamed", SaveDirTemplate = "%APPDATA%/Renamed" },
        };

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));

        var reloaded = await GameCatalog.LoadAsync(catalogPath);
        var existing = Assert.Single(reloaded.Catalog.Games);
        Assert.Equal("test-game", existing.Id);
        Assert.Equal("Test Game", existing.DisplayName);
    }

    [Fact]
    public async Task A_selection_where_every_id_collides_writes_nothing()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        var beforeBytes = await File.ReadAllBytesAsync(catalogPath);
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "test-game", DisplayName = "Colliding Game", SaveDirTemplate = "%APPDATA%/Collide" },
        };

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));

        var afterBytes = await File.ReadAllBytesAsync(catalogPath);
        Assert.Equal(beforeBytes, afterBytes);
    }

    [Fact]
    public async Task An_empty_selection_writes_nothing_and_reports_nothing_added()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        var beforeBytes = await File.ReadAllBytesAsync(catalogPath);
        dialog.FindGamesSelectionToReturn = new List<KnownGame>();

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));

        var afterBytes = await File.ReadAllBytesAsync(catalogPath);
        Assert.Equal(beforeBytes, afterBytes);
        Assert.Contains("Nothing", vm.StatusMessage);
    }

    [Fact]
    public async Task Confirming_the_same_selection_twice_adds_nothing_the_second_time()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = "%APPDATA%/A" },
        };

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));
        var afterFirst = await File.ReadAllBytesAsync(catalogPath);

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));
        var afterSecond = await File.ReadAllBytesAsync(catalogPath);

        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public async Task A_failed_write_rolls_back_every_accepted_entry()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = "%APPDATA%/A" },
        };

        using (new FileStream(catalogPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.FindGamesCommand.Execute(null);
            await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));
        }

        Assert.DoesNotContain(vm.Games, row => row.Game.Id == "game-a");
        Assert.NotEmpty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task IsScanRunning_is_false_on_construction()
    {
        var (vm, _, _) = await CreateViewModelWithSavedCatalogAsync();

        Assert.False(vm.IsScanRunning);
    }

    [Fact]
    public async Task FindGamesCommand_is_disabled_while_a_scan_runs_and_enabled_again_once_it_completes()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = "%APPDATA%/A" },
        };

        vm.FindGamesCommand.Execute(null);

        Assert.False(vm.FindGamesCommand.CanExecute(null));

        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));

        Assert.True(vm.FindGamesCommand.CanExecute(null));
    }

    [Fact]
    public async Task FindGamesCommand_is_enabled_again_after_a_cancelled_dialog()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = null;

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));

        Assert.True(vm.FindGamesCommand.CanExecute(null));
    }

    [Fact]
    public async Task FindGamesCommand_is_enabled_again_after_a_failed_write()
    {
        var (vm, dialog, catalogPath) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = "%APPDATA%/A" },
        };

        using (new FileStream(catalogPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.FindGamesCommand.Execute(null);
            await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));
        }

        Assert.True(vm.FindGamesCommand.CanExecute(null));
    }

    [Fact]
    public async Task Refresh_and_change_backup_folder_stay_enabled_while_a_scan_runs()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = "%APPDATA%/A" },
        };

        vm.FindGamesCommand.Execute(null);

        Assert.True(vm.RefreshCommand.CanExecute(null));
        Assert.True(vm.ChangeBackupRootCommand.CanExecute(null));

        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));
    }

    [Fact]
    public async Task BackupCommand_is_unaffected_by_IsScanRunning()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync(Path.Combine(_dir, "backups"));
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "game-a", DisplayName = "Game A", SaveDirTemplate = "%APPDATA%/A" },
        };

        vm.FindGamesCommand.Execute(null);

        Assert.True(vm.BackupCommand.CanExecute(vm.Games[0]));

        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null));
    }

    [Fact]
    public async Task IsBackupRunning_does_not_gate_FindGamesCommand()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync(Path.Combine(_dir, "backups"));

        bool? findGamesCanExecute = null;
        dialog.OnReport = () => findGamesCanExecute = vm.FindGamesCommand.CanExecute(null);

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => findGamesCanExecute is not null);

        Assert.True(findGamesCanExecute);
    }
}
