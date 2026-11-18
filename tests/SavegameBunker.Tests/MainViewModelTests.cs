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
    /// staging file (D-13: observed from the OnPickFolder hook, with no timing assumption).
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

    // --- The free-space advisory ------------------------------------------------------

    [Fact]
    public async Task With_a_readable_root_the_three_segments_name_the_free_figure_and_each_actions_own_total()
    {
        var (vm, _, backupRoot) = await CreateMultiGameViewModelAsync(3);

        // Isolate the "changed" set to exactly game 2 — a fresh sweep against an empty backup
        // folder would otherwise mark all three NeverBackedUp.
        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[1].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[2].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;

        await vm.RecomputeSpaceAdvisoryAsync();

        var probe = new BackupService();
        Assert.True(probe.TryGetAvailableFreeSpace(backupRoot, out var freeBytes));
        Assert.Equal(
            Strings.Format("str_freeSpace_available", SizeFormat.Describe(freeBytes)),
            vm.FreeSpaceAdvisory);

        var selectedTotal = probe.ComputeSaveDataSize(vm.Games[0].Game) + probe.ComputeSaveDataSize(vm.Games[1].Game);
        Assert.Equal(
            Strings.Format("str_freeSpace_selectedNeeds", SizeFormat.Describe(selectedTotal)),
            vm.SelectedNeedsAdvisory);

        var changedTotal = probe.ComputeSaveDataSize(vm.Games[2].Game);
        Assert.Equal(
            Strings.Format("str_freeSpace_changedNeeds", SizeFormat.Describe(changedTotal)),
            vm.ChangedNeedsAdvisory);
    }

    [Fact]
    public async Task With_no_backup_root_the_free_space_segment_is_absent_and_nothing_errors()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "");

        await vm.RecomputeSpaceAdvisoryAsync();

        Assert.Null(vm.FreeSpaceAdvisory);
        Assert.False(vm.HasFreeSpaceAdvisory);
        Assert.Empty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task An_unreadable_backup_root_leaves_the_free_space_segment_absent_and_nothing_errors()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: @"\\nonexistent-server\share\backups");

        await vm.RecomputeSpaceAdvisoryAsync();

        Assert.Null(vm.FreeSpaceAdvisory);
        Assert.Empty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task With_nothing_selected_and_nothing_changed_the_two_action_segments_are_absent()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(1);
        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));

        await vm.RecomputeSpaceAdvisoryAsync();

        Assert.Null(vm.SelectedNeedsAdvisory);
        Assert.False(vm.HasSelectedNeedsAdvisory);
        Assert.Null(vm.ChangedNeedsAdvisory);
        Assert.False(vm.HasChangedNeedsAdvisory);
    }

    [Fact]
    public async Task Recomputing_the_advisory_never_opens_the_folder_picker()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: "");
        dialog.FolderToReturn = Path.Combine(_dir, "should-not-be-used");

        await vm.RecomputeSpaceAdvisoryAsync();

        Assert.Equal(0, dialog.PickFolderCalls);
    }

    [Fact]
    public async Task A_stale_recompute_cannot_overwrite_a_newer_ones_result()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);
        vm.Games[0].IsSelected = true;

        // Both calls bump the generation synchronously (before either's own await), so the second
        // call's generation is strictly newer — whichever order the two Task.Run bodies finish in,
        // only the second call's snapshot may ever land on the properties.
        var first = vm.RecomputeSpaceAdvisoryAsync();

        vm.Games[0].IsSelected = false;
        vm.Games[1].IsSelected = true;

        var second = vm.RecomputeSpaceAdvisoryAsync();

        await Task.WhenAll(first, second);

        var expected = Strings.Format("str_freeSpace_selectedNeeds",
            SizeFormat.Describe(new BackupService().ComputeSaveDataSize(vm.Games[1].Game)));
        Assert.Equal(expected, vm.SelectedNeedsAdvisory);
    }

    [Fact]
    public async Task Both_bulk_commands_still_run_when_the_needed_total_exceeds_the_free_figure()
    {
        // Cannot actually fill a volume in a test — so this asserts only
        // that CanExecute stays true and the archives are written, never that anything was refused.
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;
        foreach (var row in vm.Games)
            row.ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[0].IsSelected = true;

        await vm.RecomputeSpaceAdvisoryAsync();

        Assert.True(vm.BackupAllChangedCommand.CanExecute(null));
        Assert.True(vm.BackupSelectedCommand.CanExecute(null));

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-0")));
    }

    [Fact]
    public async Task Recomputing_twice_around_a_completed_backup_never_carries_a_figure_between_runs()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        dialog.FolderToReturn = backupRoot;
        vm.Games[0].IsSelected = true;

        await vm.RecomputeSpaceAdvisoryAsync();
        var firstSelected = vm.SelectedNeedsAdvisory;
        Assert.NotNull(firstSelected);

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        await vm.RecomputeSpaceAdvisoryAsync();

        // The selection was left untouched by the run, and the save data itself did not
        // change size — the re-derived figure must land on the exact same sentence, proving
        // nothing was cached or carried over from the first call or from the backup that ran
        // between the two (EDGE/idempotency).
        Assert.Equal(firstSelected, vm.SelectedNeedsAdvisory);
    }

    // --- The composed StatusLine reads "needed / free", not four joined sentences -------

    [Fact]
    public async Task With_status_message_and_all_three_segments_present_the_line_reads_needed_then_free()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(3);
        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[1].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[2].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[0].IsSelected = true;

        await vm.RecomputeSpaceAdvisoryAsync();

        Assert.NotNull(vm.SelectedNeedsAdvisory);
        Assert.NotNull(vm.ChangedNeedsAdvisory);
        Assert.NotNull(vm.FreeSpaceAdvisory);

        var expected = $"{vm.StatusMessage}  ·  {vm.SelectedNeedsAdvisory} / {vm.ChangedNeedsAdvisory} / {vm.FreeSpaceAdvisory}";
        Assert.Equal(expected, vm.StatusLine);
    }

    [Fact]
    public async Task With_only_the_free_space_segment_present_the_line_contains_no_slash()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(1);
        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));

        await vm.RecomputeSpaceAdvisoryAsync();

        Assert.NotNull(vm.FreeSpaceAdvisory);
        Assert.Null(vm.SelectedNeedsAdvisory);
        Assert.Null(vm.ChangedNeedsAdvisory);

        var expected = $"{vm.StatusMessage}  ·  {vm.FreeSpaceAdvisory}";
        Assert.Equal(expected, vm.StatusLine);
        Assert.DoesNotContain(" / ", vm.StatusLine);
    }

    [Fact]
    public async Task With_all_three_advisory_segments_absent_the_line_is_exactly_the_status_message()
    {
        var (vm, _, _) = CreateViewModel(backupRoot: "");

        await vm.RecomputeSpaceAdvisoryAsync();

        Assert.Null(vm.FreeSpaceAdvisory);
        Assert.Null(vm.SelectedNeedsAdvisory);
        Assert.Null(vm.ChangedNeedsAdvisory);
        Assert.Equal(vm.StatusMessage, vm.StatusLine);
    }

    [Fact]
    public void A_rows_restore_step_text_is_empty_until_a_restore_reports_one()
    {
        var (vm, _, _) = CreateViewModel(backupRoot: null);

        Assert.All(vm.Games, row =>
        {
            Assert.Equal(0, row.RestoreStep);
            Assert.False(row.IsRestoring);
            Assert.Equal("", row.RestoreStepText);
        });
    }

    // --- Sorted insert on screen -------------------------------------------------------

    [Fact]
    public async Task Adding_a_game_whose_name_sorts_first_puts_its_row_at_index_zero()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("aaa", "AAA", Path.Combine(_dir, "aaa").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        Assert.Equal("aaa", vm.Games[0].Game.Id);
        Assert.Equal("test-game", vm.Games[1].Game.Id);
    }

    [Fact]
    public async Task Adding_a_game_whose_name_sorts_last_puts_its_row_at_the_end()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("zzz", "ZZZ", Path.Combine(_dir, "zzz").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        Assert.Equal("test-game", vm.Games[0].Game.Id);
        Assert.Equal("zzz", vm.Games[1].Game.Id);
    }

    [Fact]
    public async Task Adding_a_game_whose_name_ties_puts_the_new_row_after_the_existing_one()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("test-game-2", "Test Game", Path.Combine(_dir, "tie").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        Assert.Equal("test-game", vm.Games[0].Game.Id);
        Assert.Equal("test-game-2", vm.Games[1].Game.Id);
    }

    [Fact]
    public async Task Adding_a_game_whose_name_ties_only_by_letter_case_puts_the_new_row_after_it()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("test-game-2", "test game", Path.Combine(_dir, "case-tie").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        Assert.Equal("test-game", vm.Games[0].Game.Id);
        Assert.Equal("test-game-2", vm.Games[1].Game.Id);
    }

    [Fact]
    public async Task A_scan_that_adds_several_games_places_each_at_its_own_sorted_position()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "zebra", DisplayName = "Zebra", SaveDirTemplate = "%APPDATA%/Zebra" },
            new() { Id = "alpha", DisplayName = "Alpha", SaveDirTemplate = "%APPDATA%/Alpha" },
        };

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null) && vm.Games.Count == 3);

        Assert.Equal(["alpha", "test-game", "zebra"], vm.Games.Select(row => row.Game.Id));
    }

    [Fact]
    public async Task A_checked_row_stays_checked_after_an_add_repositions_it()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        vm.Games[0].IsSelected = true;
        dialog.AddGameToReturn = new AddGameResult("aaa", "AAA", Path.Combine(_dir, "aaa-sel").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        var original = Assert.Single(vm.Games, row => row.Game.Id == "test-game");
        Assert.True(original.IsSelected);
        Assert.False(vm.Games.Single(row => row.Game.Id == "aaa").IsSelected);
    }

    [Fact]
    public async Task Change_save_folder_repairs_the_row_it_was_opened_from_when_on_screen_order_differs_from_the_catalog()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("aaa", "AAA", Path.Combine(_dir, "aaa-csf").Replace('\\', '/'));
        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        var original = Assert.Single(vm.Games, row => row.Game.Id == "test-game");
        var neighbour = Assert.Single(vm.Games, row => row.Game.Id == "aaa");
        var originalTemplate = original.Game.SaveDirTemplate;
        var neighbourTemplate = neighbour.Game.SaveDirTemplate;
        var newFolder = Path.Combine(_dir, "moved-after-sort");
        Directory.CreateDirectory(newFolder);
        dialog.ChangeSaveFolderToReturn = newFolder;

        vm.ChangeSaveFolderCommand.Execute(original);
        await WaitForAsync(() => vm.ChangeSaveFolderCommand.CanExecute(original));

        var repaired = Assert.Single(vm.Games, row => row.Game.Id == "test-game");
        Assert.Equal(newFolder, repaired.Game.SaveDirTemplate);
        Assert.Equal(neighbourTemplate, Assert.Single(vm.Games, row => row.Game.Id == "aaa").Game.SaveDirTemplate);
        Assert.NotEqual(originalTemplate, repaired.Game.SaveDirTemplate);
    }

    [Fact]
    public async Task Refresh_over_an_unsorted_file_still_shows_the_rows_in_file_order()
    {
        var catalogPath = Path.Combine(_dir, "unsorted-games.json");
        File.WriteAllText(catalogPath, """
            {
              "backupRoot": "E:/SavegameBunkerBackups",
              "games": [
                { "id": "b", "displayName": "B", "saveDirTemplate": "%APPDATA%/B" },
                { "id": "a", "displayName": "A", "saveDirTemplate": "%APPDATA%/A" }
              ]
            }
            """);
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, new RecordingDialogService());

        Assert.Equal(["b", "a"], vm.Games.Select(row => row.Game.Id));

        await WaitForAsync(() => vm.RefreshCommand.CanExecute(null));
        vm.RefreshCommand.Execute(null);
        await WaitForAsync(() => vm.RefreshCommand.CanExecute(null));

        Assert.Equal(["b", "a"], vm.Games.Select(row => row.Game.Id));
    }

    // --- Drop folder into Add Game ---------------------------------------------------

    [Fact]
    public async Task Dropping_a_folder_opens_add_game_with_that_path_and_does_not_guess_a_name()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        var folder = Path.Combine(_dir, "dropped-saves");
        Directory.CreateDirectory(folder);

        await vm.AddGameFromDroppedFolderAsync(folder);

        Assert.Equal(folder, Assert.Single(dialog.ShowAddGameInitialFolders));
        Assert.Empty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task Toolbar_add_game_still_opens_the_dialog_without_a_prefilled_folder()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => dialog.ShowAddGameInitialFolders.Count == 1);

        Assert.Null(Assert.Single(dialog.ShowAddGameInitialFolders));
        Assert.Empty(dialog.ErrorMessages);
    }

    // --- Startup backup-root health --------------------------------------------------

    [Fact]
    public async Task ReportLoad_tells_the_user_when_a_configured_backup_root_is_unreachable()
    {
        var missing = Path.Combine(_dir, "offline-backups");
        var (vm, dialog, _) = CreateViewModel(missing);

        await vm.ReportLoad();

        Assert.NotEmpty(dialog.ErrorMessages);
        Assert.Contains("unreachable", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unreachable", dialog.ErrorMessages[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportLoad_stays_quiet_when_backup_root_is_unset()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: null);

        await vm.ReportLoad();

        Assert.Empty(dialog.ErrorMessages);
        Assert.DoesNotContain("unreachable", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportLoad_does_not_add_a_health_dialog_on_the_fatal_path()
    {
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(
            Path.Combine(_dir, "broken.json"),
            new Catalog(),
            [],
            Seeded: false,
            FatalError: "games.json could not be parsed.");
        var vm = new MainViewModel(load, dialog);

        await vm.ReportLoad();

        Assert.Equal("games.json could not be parsed.", vm.StatusMessage);
        Assert.DoesNotContain(
            "unreachable",
            string.Join('\n', dialog.ErrorMessages),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportLoad_skips_health_when_the_catalog_was_seeded()
    {
        var missing = Path.Combine(_dir, "template-backups");
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(
            Path.Combine(_dir, "games.json"),
            new Catalog { BackupRoot = missing },
            [],
            Seeded: true,
            FatalError: null);
        var vm = new MainViewModel(load, dialog);

        await vm.ReportLoad();

        Assert.Empty(dialog.ErrorMessages);
        Assert.DoesNotContain("unreachable", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}
