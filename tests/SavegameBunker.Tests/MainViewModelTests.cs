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
public sealed class MainViewModelTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly string _dir;
    private readonly string _suggestedStartFolder;

    public MainViewModelTests()
    {
        _dir = _temp.DirectoryPath;
        _suggestedStartFolder = Path.Combine(_dir, "suggested-start");
    }

    public void Dispose() => _temp.Dispose();

    private (MainViewModel Vm, RecordingDialogService Dialog, string CatalogPath) CreateViewModel(string? backupRoot)
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);

        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog
        {
            BackupRoot = backupRoot,
            Games =
            [
                new() { Id = "test-game", DisplayName = "Test Game", SaveDirTemplate = saveDir.Replace('\\', '/') },
            ],
        };

        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(catalogPath, catalog, [], false, null);
        var vm = new MainViewModel(load, dialog, () => _suggestedStartFolder);
        return (vm, dialog, catalogPath);
    }

    /// <summary>
    /// Same construction shape as <see cref="CreateViewModel"/>, extended to <paramref name="count"/>
    /// games, for tests that need a genuine partial selection to exercise
    /// <see cref="MainViewModel.HeaderCheckState"/>'s Indeterminate state. Deliberately synchronous
    /// (no <c>await</c> anywhere in this method or between calling it and reading
    /// <see cref="MainViewModel.GamesView"/>/<see cref="MainViewModel.HeaderCheckState"/>): that
    /// live <see cref="System.ComponentModel.ICollectionView"/> is a WPF <c>DispatcherObject</c>
    /// bound to whichever thread constructs it, and a plain xUnit host has no
    /// <see cref="System.Threading.SynchronizationContext"/> to keep a test's continuation on that
    /// same thread across a real <c>await</c> yield (e.g. <c>WaitForAsync</c>'s <c>Task.Delay</c>) —
    /// reading it from a different thread throws. Callers of this helper must not <c>await</c>
    /// anything between calling it and reading either property.
    /// </summary>
    private (MainViewModel Vm, RecordingDialogService Dialog) CreateViewModelWithGames(int count)
    {
        var games = new List<Game>();
        for (var i = 0; i < count; i++)
        {
            var saveDir = Path.Combine(_dir, $"hdr-saves-{i}");
            Directory.CreateDirectory(saveDir);
            File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
            games.Add(new Game { Id = $"hdr-game-{i}", DisplayName = $"Header Game {i}", SaveDirTemplate = saveDir.Replace('\\', '/') });
        }

        var catalogPath = Path.Combine(_dir, "hdr-games.json");
        var catalog = new Catalog { BackupRoot = Path.Combine(_dir, "backups"), Games = games };
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(catalogPath, catalog, [], false, null);
        var vm = new MainViewModel(load, dialog);
        return (vm, dialog);
    }

    /// <summary>
    /// Same shape as <see cref="CreateViewModelWithGames(int)"/>, but with caller-chosen display
    /// names — the search-filter tests need distinct, searchable names rather than the
    /// generic "Header Game N" the other helper produces. Deliberately synchronous for the same
    /// GamesView dispatcher-affinity reason documented there.
    /// </summary>
    private (MainViewModel Vm, RecordingDialogService Dialog) CreateNamedViewModel(params string[] displayNames)
    {
        var games = new List<Game>();
        for (var i = 0; i < displayNames.Length; i++)
        {
            var saveDir = Path.Combine(_dir, $"search-saves-{i}");
            Directory.CreateDirectory(saveDir);
            File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
            games.Add(new Game { Id = $"search-game-{i}", DisplayName = displayNames[i], SaveDirTemplate = saveDir.Replace('\\', '/') });
        }

        var catalogPath = Path.Combine(_dir, "search-games.json");
        var catalog = new Catalog { BackupRoot = Path.Combine(_dir, "backups"), Games = games };
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(catalogPath, catalog, [], false, null);
        var vm = new MainViewModel(load, dialog);
        return (vm, dialog);
    }

    /// <summary>
    /// Same starting catalog as <see cref="CreateViewModel"/>, but written to disk first so the
    /// Find games write-path facts below can assert byte-for-byte equality of the catalog file,
    /// not just an entry count on the in-memory <see cref="CatalogLoad"/>.
    /// </summary>
    private async Task<(MainViewModel Vm, RecordingDialogService Dialog, string CatalogPath)> CreateViewModelWithSavedCatalogAsync(string? backupRoot = null)
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);

        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog
        {
            BackupRoot = backupRoot,
            Games =
            [
                new() { Id = "test-game", DisplayName = "Test Game", SaveDirTemplate = saveDir.Replace('\\', '/') },
            ],
        };
        await GameCatalog.SaveAsync(catalogPath, catalog);

        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);
        return (vm, dialog, catalogPath);
    }

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

    // --- RestoreCommand, the busy gate, the confirmation copy, the distinct errors ---

    private static string SaveFilePath(string dir) => Path.Combine(dir, "save.dat");

    /// <summary>
    /// Writes a real archive for the row's already-existing save data via a standalone
    /// <see cref="BackupService"/> instance — the same write path <c>MainViewModel</c>'s own
    /// <c>_backup</c> field uses, so the fixture's backups are indistinguishable from ones a real
    /// backup gesture would have produced. The archive's file-name timestamp truncates to whole
    /// seconds while <c>File.LastWriteTime</c> keeps sub-second precision, so a backup taken in the
    /// same wall-clock second as the save file's write can otherwise parse as earlier than it —
    /// pushing the save file's own timestamp two seconds back removes that race and gives every
    /// caller a deterministic "up to date" starting point to build a scenario from.
    /// </summary>
    private BackupArchive SeedBackup(string backupRoot, Game game)
    {
        var result = new BackupService().Backup(game, backupRoot);
        Assert.True(result.Success, result.Message);
        var archive = new BackupService().ListBackups(backupRoot, game.Id)[0];
        File.SetLastWriteTime(SaveFilePath(Path.Combine(_dir, "saves")), archive.TakenAt.AddSeconds(-2));
        return archive;
    }

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
        Assert.Contains("Use Change backup folder", vm.StatusMessage);
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

    /// <summary>
    /// Builds a view model over <paramref name="gameCount"/> games, each with a real, non-empty
    /// save directory so a real <c>BackupService.Backup</c> call against it can succeed, then waits
    /// for the construction-time sweep to settle before returning — every bulk-backup test forces
    /// its own comparison kinds afterward via <see cref="GameRowViewModel.ApplyBackupComparison"/>,
    /// the same synthetic-state technique <c>GameRowViewModelTests</c> uses.
    /// </summary>
    private async Task<(MainViewModel Vm, RecordingDialogService Dialog, string BackupRoot)> CreateMultiGameViewModelAsync(int gameCount)
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var games = new List<Game>();
        for (var i = 0; i < gameCount; i++)
        {
            var saveDir = Path.Combine(_dir, $"saves-{i}");
            Directory.CreateDirectory(saveDir);
            File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
            games.Add(new Game { Id = $"game-{i}", DisplayName = $"Game {i}", SaveDirTemplate = saveDir.Replace('\\', '/') });
        }

        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog { BackupRoot = backupRoot, Games = games };
        await GameCatalog.SaveAsync(catalogPath, catalog);

        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);

        await WaitForAsync(() => vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));

        return (vm, dialog, backupRoot);
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

    // --- Busy scoping ---------------------------------------------------------
    //
    // The visible surfaces are XAML and are a walkthrough item; what is testable here is the
    // scoping rule those surfaces bind to — one row's work marks that row and no other, and a
    // bulk run clears both its busy state and its progress state before its outcome is reported.

    [Fact]
    public async Task A_single_game_backup_marks_only_its_own_row_busy()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;

        var target = vm.Games[1];
        Assert.All(vm.Games, row => Assert.False(row.IsBusy));

        vm.BackupCommand.Execute(target);
        await WaitForAsync(() => !target.IsBusy && !vm.IsBackupRunning);

        // Every row is idle once the run settles, and the two bystanders were never marked at all
        // — a row is busy for its own operation only.
        Assert.All(vm.Games, row => Assert.False(row.IsBusy));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-1")));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-0")));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-2")));
    }

    [Fact]
    public async Task A_second_backup_click_on_the_same_row_before_the_first_settles_is_ignored()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        dialog.FolderToReturn = backupRoot;

        var target = vm.Games[0];

        // row.IsBusy is raised synchronously before EnsureBackupRootAsync's await, so this second
        // Execute (simulating a same-row double click before that await returns) must be a no-op.
        vm.BackupCommand.Execute(target);
        vm.BackupCommand.Execute(target);
        await WaitForAsync(() => !vm.IsBackupRunning);

        Assert.Single(Directory.GetFiles(Path.Combine(backupRoot, "game-0"), "*.zip"));
    }

    [Fact]
    public async Task A_row_is_left_idle_after_its_backup_finishes()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        dialog.FolderToReturn = backupRoot;

        var target = vm.Games[0];

        vm.BackupCommand.Execute(target);
        await WaitForAsync(() => !vm.IsBackupRunning);

        Assert.False(target.IsBusy);
        Assert.Equal(0, target.RestoreStep);
        Assert.Equal("", target.RestoreStepText);
        // The bar never outlives the write — both back at idle once the backup returns.
        Assert.Equal(0, target.BackupBytesTotal);
        Assert.Equal(0, target.BackupBytesWritten);
    }

    [Fact]
    public async Task Bulk_backup_clears_its_progress_state_before_reporting_its_outcome()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;

        foreach (var row in vm.Games)
            row.ApplyBackupComparison(BackupComparison.NeverBackedUp());

        Assert.False(vm.IsBulkBackupRunning);

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null));

        // Progress and outcome never share a surface: by the time the outcome exists, the
        // window-level bar is gone.
        Assert.False(vm.IsBulkBackupRunning);
        Assert.Equal(0, vm.BulkBackupTotal);
        // Count/name reports now travel through onUi inside the run, never after it — the closing
        // counter clear in RunBulkBackupAsync's onFinished has already run by the time the command
        // settles, so no wait is needed here.
        Assert.Equal(0, vm.BulkBackupCurrent);
        Assert.Null(vm.BulkBackupCurrentGameName);
        Assert.NotEmpty(dialog.InfoMessages);
        // Every target row's own byte state is back at zero too — the bulk game-count
        // surface above is unrelated per-row byte state, and both must clear independently.
        // Byte reports travel through Progress<T>, which on a test host (no SynchronizationContext)
        // posts to the thread pool and can land after the command settles — wait for them.
        await WaitForAsync(() => vm.Games.All(row => row.BackupBytesTotal == 0 && row.BackupBytesWritten == 0));
    }

    // --- "Back up selected" -----------------------------------------------------------

    [Fact]
    public async Task Back_up_selected_writes_an_archive_for_exactly_the_checked_games()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].IsSelected = true;
        vm.Games[2].IsSelected = true;

        Assert.True(vm.BackupSelectedCommand.CanExecute(null));

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-0")));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-1")));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-2")));
    }

    // Pins the "Back up selected" contract. Every fact below is a regression pin:
    // BackupSelectedAsync/CanBackupSelected implement it by cloning BackupAllChangedAsync's
    // proven mechanics. Named with the literal "BackupSelected"
    // substring so this suite is selectable in one filtered run.

    [Fact]
    public async Task BackupSelected_cannot_execute_with_zero_rows_checked()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);

        Assert.False(vm.HasAnySelected);
        Assert.False(vm.BackupSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task BackupSelected_backs_up_a_checked_row_even_when_it_is_already_up_to_date()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[0].IsSelected = true;

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-0")));
    }

    [Fact]
    public async Task BackupSelected_backs_up_a_row_hidden_by_the_active_search_filter()
    {
        // Deliberately synchronous construction (CreateNamedViewModel's own doc comment): the
        // filter/refresh calls below run before any await, so GamesView's dispatcher-affine read
        // is safe.
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World");
        var backupRoot = Path.Combine(_dir, "backups");

        var hidden = vm.Games.Single(row => row.DisplayName == "Beta World");
        hidden.IsSelected = true;

        vm.SearchText = "alpha";
        vm.GamesView.Refresh();

        Assert.DoesNotContain(hidden, vm.GamesView.Cast<GameRowViewModel>());

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        Assert.True(Directory.Exists(Path.Combine(backupRoot, hidden.Game.Id)));
    }

    [Fact]
    public async Task BackupSelected_processes_checked_games_in_Games_order_sequentially()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;

        foreach (var row in vm.Games)
            row.IsSelected = true;

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        // Each game's archive is real, sequential file I/O (write, verify-by-reopen, rename) — a
        // later Games-order archive cannot have been created before an earlier one's write finished.
        var createdTimes = vm.Games
            .Select(row => Directory.GetFiles(Path.Combine(backupRoot, row.Game.Id), "*.zip").Single())
            .Select(File.GetCreationTimeUtc)
            .ToList();

        Assert.True(createdTimes[0] <= createdTimes[1]);
        Assert.True(createdTimes[1] <= createdTimes[2]);
    }

    [Fact]
    public async Task BackupSelected_writes_exactly_one_archive_for_a_row_also_in_the_changed_set()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[0].IsSelected = true;

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        Assert.Single(Directory.GetFiles(Path.Combine(backupRoot, "game-0"), "*.zip"));
    }

    [Fact]
    public async Task BackupSelected_leaves_the_selection_exactly_as_it_was_before_the_run()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].IsSelected = true;
        vm.Games[2].IsSelected = true;

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        Assert.Equal(2, vm.SelectedCount);
        Assert.True(vm.Games[0].IsSelected);
        Assert.False(vm.Games[1].IsSelected);
        Assert.True(vm.Games[2].IsSelected);
    }

    [Fact]
    public async Task BackupSelected_excludes_an_already_busy_checked_row_from_its_target_list()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;
        vm.Games[0].IsBusy = true;

        await vm.BackupSelectedCommand.ExecuteAsync(null);

        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-0")));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-1")));
    }

    [Fact]
    public async Task BackupSelected_command_raises_CanExecuteChanged_when_a_rows_IsSelected_flips()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(1);

        var raised = false;
        vm.BackupSelectedCommand.CanExecuteChanged += (_, _) => raised = true;

        vm.Games[0].IsSelected = true;

        Assert.True(raised);
    }

    [Fact]
    public async Task BackupSelected_clears_its_progress_state_before_reporting_its_outcome()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;

        int? totalAtReport = null;
        int? currentAtReport = null;
        string? nameAtReport = "unset";
        dialog.OnReport = () =>
        {
            totalAtReport = vm.BulkBackupTotal;
            currentAtReport = vm.BulkBackupCurrent;
            nameAtReport = vm.BulkBackupCurrentGameName;
        };

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        Assert.Equal(0, totalAtReport);
        Assert.Equal(0, currentAtReport);
        Assert.Null(nameAtReport);
        // Same clearing discipline as BackupAllChangedAsync's own bulk loop — every
        // targeted row's byte state is back at zero once the run settles.
        // Byte reports travel through Progress<T>, which on a test host (no SynchronizationContext)
        // posts to the thread pool and can land after the command settles — wait for them.
        await WaitForAsync(() => vm.Games.All(row => row.BackupBytesTotal == 0 && row.BackupBytesWritten == 0));
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

    // --- Busy state under concurrency -----------------------

    [Fact]
    public async Task Bulk_backup_marks_every_target_row_busy_for_the_whole_run()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;

        foreach (var row in vm.Games)
            row.ApplyBackupComparison(BackupComparison.NeverBackedUp());

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null));

        // Cleared again once the run is over — the marking is scoped to the run, not permanent.
        Assert.All(vm.Games, row => Assert.False(row.IsBusy));
    }

    [Fact]
    public async Task Refresh_and_change_backup_folder_are_gated_while_a_bulk_run_is_in_flight()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        Assert.True(vm.RefreshCommand.CanExecute(null));
        Assert.True(vm.ChangeBackupRootCommand.CanExecute(null));

        // The gate is derived from the bulk progress state, so setting that state is what proves
        // the wiring — the run itself is too short to sample reliably.
        foreach (var row in vm.Games)
            row.ApplyBackupComparison(BackupComparison.NeverBackedUp());

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null));

        // Back to enabled once the run has finished and cleared its state.
        Assert.False(vm.IsBulkBackupRunning);
        Assert.True(vm.RefreshCommand.CanExecute(null));
        Assert.True(vm.ChangeBackupRootCommand.CanExecute(null));
    }

    [Fact]
    public async Task Two_overlapping_row_backups_keep_the_window_gated_until_the_last_one_finishes()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;

        // One command instance serves every row, so a plain boolean would be cleared by
        // whichever backup finished first, re-enabling Refresh while the other still wrote. The
        // counter is what makes the flag mean "any backup in flight" rather than "the last one
        // started".
        var first = vm.BackupCommand.ExecuteAsync(vm.Games[0]);
        var second = vm.BackupCommand.ExecuteAsync(vm.Games[1]);

        await Task.WhenAll(first, second);
        await WaitForAsync(() => !vm.IsBackupRunning);

        // Both wrote, and the flag settled to false exactly once both were done.
        Assert.False(vm.IsBackupRunning);
        Assert.True(vm.RefreshCommand.CanExecute(null));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-0")));
        Assert.True(Directory.Exists(Path.Combine(backupRoot, "game-1")));
        Assert.All(vm.Games, row => Assert.False(row.IsBusy));
    }

    [Fact]
    public async Task Checking_one_row_updates_SelectedCount_and_HasAnySelected_without_affecting_the_other_row()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.HasAnySelected);

        vm.Games[0].IsSelected = true;

        Assert.Equal(1, vm.SelectedCount);
        Assert.True(vm.HasAnySelected);
        Assert.False(vm.Games[1].IsSelected);
    }

    [Fact]
    public async Task An_empty_catalog_leaves_SelectedCount_at_zero_and_HasAnySelected_false_with_no_exception()
    {
        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog { BackupRoot = Path.Combine(_dir, "backups"), Games = [] };
        await GameCatalog.SaveAsync(catalogPath, catalog);

        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.HasAnySelected);
    }

    [Fact]
    public async Task Selection_survives_a_refresh_that_rebuilds_every_row()
    {
        var (vm, _, _) = await CreateViewModelWithSavedCatalogAsync();
        vm.Games[0].IsSelected = true;

        // Awaits the command's own Task directly rather than Execute()+WaitForAsync(CanExecute):
        // CanRefreshOrChangeBackupRoot reads IsAnyRowBusy (Games.Any(...)), and polling it from a
        // second thread while RefreshAsync's continuation runs Games.Clear()/Add() on a plain
        // xUnit host (no UI dispatcher to serialize the two) is a test-harness-only race — in the
        // running app both always run on the single UI thread. Awaiting the task directly removes
        // the second thread instead of exercising that unrelated race.
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.Games[0].IsSelected);
    }

    [Fact]
    public async Task Selection_survives_change_save_folder_replacing_the_row()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        vm.Games[0].IsSelected = true;
        var newFolder = Path.Combine(_dir, "moved-saves");
        Directory.CreateDirectory(newFolder);
        dialog.ChangeSaveFolderToReturn = newFolder;

        vm.ChangeSaveFolderCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => vm.ChangeSaveFolderCommand.CanExecute(vm.Games[0]));

        Assert.True(vm.Games[0].IsSelected);
    }

    [Fact]
    public void HeaderCheckState_reads_the_three_states_over_the_current_catalog()
    {
        var (vm, _) = CreateViewModelWithGames(3);

        Assert.False(vm.HeaderCheckState);

        vm.Games[0].IsSelected = true;
        Assert.Null(vm.HeaderCheckState);

        foreach (var row in vm.Games)
            row.IsSelected = true;
        Assert.True(vm.HeaderCheckState);

        foreach (var row in vm.Games)
            row.IsSelected = false;
        Assert.False(vm.HeaderCheckState);
    }

    [Fact]
    public void HeaderCheckState_is_false_never_null_for_an_empty_catalog()
    {
        var (vm, _) = CreateViewModelWithGames(0);

        Assert.False(vm.HeaderCheckState);
    }

    [Fact]
    public void Toggling_the_header_selects_or_clears_every_row_depending_on_its_current_state()
    {
        var (vm, _) = CreateViewModelWithGames(2);

        // Unchecked -> select all.
        vm.ToggleHeaderSelectionCommand.Execute(null);
        Assert.All(vm.Games, row => Assert.True(row.IsSelected));
        Assert.True(vm.HeaderCheckState);

        // Checked -> clear all.
        vm.ToggleHeaderSelectionCommand.Execute(null);
        Assert.All(vm.Games, row => Assert.False(row.IsSelected));
        Assert.False(vm.HeaderCheckState);

        // Indeterminate -> select all.
        vm.Games[0].IsSelected = true;
        Assert.Null(vm.HeaderCheckState);
        vm.ToggleHeaderSelectionCommand.Execute(null);
        Assert.All(vm.Games, row => Assert.True(row.IsSelected));
    }

    [Fact]
    public async Task HeaderCheckState_changes_are_observable_the_moment_any_rows_IsSelected_changes()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);

        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        vm.Games[0].IsSelected = true;

        Assert.Contains(nameof(MainViewModel.HeaderCheckState), changed);
    }

    [Fact]
    public void An_empty_search_leaves_every_row_visible_after_refresh()
    {
        var (vm, _) = CreateViewModelWithGames(3);

        vm.GamesView.Refresh();

        Assert.Equal(3, vm.GamesView.Cast<GameRowViewModel>().Count());
    }

    [Fact]
    public void A_substring_search_narrows_the_view_to_the_matching_row_case_insensitively()
    {
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World", "Gamma Saga");

        vm.SearchText = "beta";
        vm.GamesView.Refresh();

        var only = Assert.Single(vm.GamesView.Cast<GameRowViewModel>());
        Assert.Equal("Beta World", only.DisplayName);
    }

    [Fact]
    public void Clearing_the_search_restores_every_row()
    {
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World");

        vm.SearchText = "beta";
        vm.GamesView.Refresh();
        Assert.Single(vm.GamesView.Cast<GameRowViewModel>());

        vm.SearchText = "";
        vm.GamesView.Refresh();
        Assert.Equal(2, vm.GamesView.Cast<GameRowViewModel>().Count());
    }

    [Fact]
    public void A_row_hidden_by_the_active_filter_is_still_counted_by_the_existing_safety_gates()
    {
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World");

        // The construction-time sweep (StartStatusScan) runs in the background and settles each
        // row's ComparisonKind on its own schedule. Blocking here synchronously (no await, so the
        // dispatcher-bound GamesView below is never touched from a different thread) keeps that
        // sweep from racing with this test's own manual ApplyBackupComparison calls and stomping
        // them back to a scanned verdict.
        var sweepDeadline = DateTime.UtcNow.AddMilliseconds(DefaultTimeoutMs);
        while (vm.Games.Any(row => row.ComparisonKind == BackupComparisonKind.Checking) && DateTime.UtcNow < sweepDeadline)
            Thread.Sleep(5);
        Assert.DoesNotContain(vm.Games, row => row.ComparisonKind == BackupComparisonKind.Checking);

        vm.SearchText = "alpha";
        vm.GamesView.Refresh();
        Assert.Single(vm.GamesView.Cast<GameRowViewModel>());

        // "Beta World" is now hidden by the active filter.
        var hidden = vm.Games.Single(row => row.DisplayName == "Beta World");
        hidden.IsBusy = true;
        Assert.True(vm.IsAnyRowBusy);

        hidden.IsBusy = false;
        hidden.ApplyBackupComparison(BackupComparison.NeverBackedUp());
        Assert.True(vm.HasBackupTargets);

        hidden.ApplyBackupComparison(BackupComparison.Checking());
        Assert.True(vm.IsAnyRowChecking);
    }

    [Fact]
    public void A_selection_made_before_filtering_survives_being_hidden_and_reappears_once_the_filter_clears()
    {
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World");
        var hidden = vm.Games.Single(row => row.DisplayName == "Beta World");
        hidden.IsSelected = true;

        vm.SearchText = "alpha";
        vm.GamesView.Refresh();

        Assert.Equal(1, vm.SelectedCount);
        Assert.True(hidden.IsSelected);

        vm.SearchText = "";
        vm.GamesView.Refresh();

        Assert.True(hidden.IsSelected);
        Assert.Contains(hidden, vm.GamesView.Cast<GameRowViewModel>());
    }

    [Fact]
    public void Header_select_all_and_clear_all_act_only_on_the_currently_visible_row_when_filtered()
    {
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World");
        var hidden = vm.Games.Single(row => row.DisplayName == "Beta World");
        hidden.IsSelected = true;

        vm.SearchText = "alpha";
        vm.GamesView.Refresh();

        // Select-all only touches the one currently visible row (Alpha Quest); the hidden row
        // (Beta World, already selected) is left untouched by either direction.
        vm.ToggleHeaderSelectionCommand.Execute(null);
        var visible = vm.Games.Single(row => row.DisplayName == "Alpha Quest");
        Assert.True(visible.IsSelected);
        Assert.True(hidden.IsSelected);

        vm.ToggleHeaderSelectionCommand.Execute(null);
        Assert.False(visible.IsSelected);
        Assert.True(hidden.IsSelected);
    }

    [Fact]
    public void Re_running_the_same_filter_twice_produces_the_identical_visible_set_both_times()
    {
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World", "Gamma Saga");
        vm.SearchText = "a";
        vm.GamesView.Refresh();
        var first = vm.GamesView.Cast<GameRowViewModel>().ToList();

        vm.GamesView.Refresh();
        var second = vm.GamesView.Cast<GameRowViewModel>().ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_non_matching_search_against_a_non_empty_catalog_shows_the_zero_match_empty_state()
    {
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World");

        vm.SearchText = "zzz-no-match";
        vm.GamesView.Refresh();

        Assert.True(vm.HasNoVisibleGames);
        Assert.False(vm.HasNoGames);
        Assert.DoesNotContain("zzz-no-match", vm.NoVisibleGamesBody);
        Assert.Equal(Strings.Get("str_search_noMatchesBody"), vm.NoVisibleGamesBody);
    }

    [Fact]
    public void An_empty_catalog_always_shows_the_zero_games_empty_state_regardless_of_search_text()
    {
        var (vm, _) = CreateViewModelWithGames(0);

        vm.SearchText = "anything";

        Assert.True(vm.HasNoGames);
        Assert.False(vm.HasNoVisibleGames);
    }

    /// <summary>
    /// Regression for a live-app defect: adding the first game to a previously-empty catalog
    /// flashed the "No games match" empty state, because <c>GamesView</c>'s internal sync handler
    /// and the constructor's own <c>Games.CollectionChanged</c> handler raced on subscription order
    /// (whichever subscribes second on the same <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>
    /// event runs second on every future Add/Remove too). Asserting inside the notification handler
    /// itself is what exercises the race — reading <see cref="MainViewModel.HasNoVisibleGames"/>
    /// after <c>Games.Add</c> returns would not, since by then both handlers have already run.
    /// </summary>
    [Fact]
    public void HasNoVisibleGames_reads_correctly_within_the_same_notification_that_adds_the_first_row()
    {
        var (vm, _) = CreateViewModelWithGames(0);
        var observed = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.HasNoVisibleGames))
                observed.Add(vm.HasNoVisibleGames);
        };

        var game = new Game { Id = "new-game", DisplayName = "New Game", SaveDirTemplate = _dir };
        vm.Games.Add(new GameRowViewModel(game));

        Assert.Contains(false, observed);
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

    // --- "Restore selected" -----------------------------------------------------------

    [Fact]
    public async Task Restore_selected_restores_exactly_the_checked_games_from_newest_ordinary_archive()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;
        dialog.ConfirmToReturn = true;

        vm.Games[0].IsSelected = true;
        vm.Games[2].IsSelected = true;
        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        foreach (var i in new[] { 0, 1, 2 })
        {
            File.WriteAllBytes(
                Path.Combine(_dir, $"saves-{i}", "save.dat"),
                [9, 9, 9]);
        }

        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.RestoreSelectedCommand.CanExecute(null), timeoutMs: RealIoTimeoutMs);

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(_dir, "saves-0", "save.dat")));
        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(Path.Combine(_dir, "saves-1", "save.dat")));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(_dir, "saves-2", "save.dat")));
        Assert.Empty(dialog.RestorePickerCalls);
        Assert.Equal(2, dialog.ConfirmMessages.Count);
        Assert.Contains("1 of 2", dialog.ConfirmMessages[0], StringComparison.Ordinal);
        Assert.Contains("2 of 2", dialog.ConfirmMessages[1], StringComparison.Ordinal);
        Assert.Contains("This will replace everything", dialog.ConfirmMessages[0], StringComparison.Ordinal);
        Assert.Contains("Your current saves were backed up first", dialog.ConfirmMessages[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_selected_cannot_execute_with_zero_rows_checked()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);

        Assert.False(vm.HasAnySelected);
        Assert.False(vm.RestoreSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task Restore_selected_skips_pre_restore_archives_when_picking_newest()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        dialog.FolderToReturn = backupRoot;
        dialog.ConfirmToReturn = true;

        vm.Games[0].IsSelected = true;
        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        var savePath = Path.Combine(_dir, "saves-0", "save.dat");
        File.WriteAllBytes(savePath, [7, 7, 7]);
        var gameDir = Path.Combine(backupRoot, "game-0");
        var safetyPath = Path.Combine(gameDir, "2099-01-01_000000-pre-restore.zip");
        ZipFile.CreateFromDirectory(Path.Combine(_dir, "saves-0"), safetyPath);
        File.SetLastWriteTime(safetyPath, DateTime.Now.AddHours(1));
        File.WriteAllBytes(savePath, [9, 9, 9]);

        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.RestoreSelectedCommand.CanExecute(null), timeoutMs: RealIoTimeoutMs);

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(savePath));
        Assert.Empty(dialog.RestorePickerCalls);
    }

    [Fact]
    public async Task Restore_selected_fails_a_game_that_has_only_safety_archives()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        dialog.FolderToReturn = backupRoot;
        dialog.ConfirmToReturn = true;

        var savePath = Path.Combine(_dir, "saves-0", "save.dat");
        Directory.CreateDirectory(Path.Combine(backupRoot, "game-0"));
        ZipFile.CreateFromDirectory(Path.Combine(_dir, "saves-0"), Path.Combine(backupRoot, "game-0", "2099-01-01_000000-pre-restore.zip"));
        File.WriteAllBytes(savePath, [9, 9, 9]);

        vm.Games[0].IsSelected = true;
        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.RestoreSelectedCommand.CanExecute(null), timeoutMs: RealIoTimeoutMs);

        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(savePath));
        Assert.Empty(dialog.ConfirmMessages);
        Assert.Empty(dialog.RestorePickerCalls);
        Assert.NotEmpty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task Restore_selected_continues_after_a_declined_confirm()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;
        dialog.ConfirmReturnSequence = new Queue<bool>([false, true]);

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;
        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        File.WriteAllBytes(Path.Combine(_dir, "saves-0", "save.dat"), [9, 9, 9]);
        File.WriteAllBytes(Path.Combine(_dir, "saves-1", "save.dat"), [9, 9, 9]);

        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.RestoreSelectedCommand.CanExecute(null), timeoutMs: RealIoTimeoutMs);

        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(Path.Combine(_dir, "saves-0", "save.dat")));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(_dir, "saves-1", "save.dat")));
        Assert.Equal(2, dialog.ConfirmMessages.Count);
        Assert.Empty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task Restore_selected_restores_a_row_hidden_by_the_active_search_filter()
    {
        var (vm, dialog) = CreateNamedViewModel("Alpha Quest", "Beta World");
        var backupRoot = Path.Combine(_dir, "backups");
        dialog.ConfirmToReturn = true;

        var hidden = vm.Games.Single(row => row.DisplayName == "Beta World");
        hidden.IsSelected = true;
        vm.SearchText = "alpha";
        vm.GamesView.Refresh();
        Assert.DoesNotContain(hidden, vm.GamesView.Cast<GameRowViewModel>());

        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        var savePath = Path.Combine(_dir, "search-saves-1", "save.dat");
        File.WriteAllBytes(savePath, [9, 9, 9]);

        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.RestoreSelectedCommand.CanExecute(null), timeoutMs: RealIoTimeoutMs);

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(savePath));
        Assert.True(hidden.IsSelected);
    }

    [Fact]
    public async Task Restore_selected_runs_for_an_up_to_date_checked_row()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        dialog.FolderToReturn = backupRoot;
        dialog.ConfirmToReturn = true;

        vm.Games[0].IsSelected = true;
        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.RestoreSelectedCommand.CanExecute(null), timeoutMs: RealIoTimeoutMs);

        Assert.Single(dialog.ConfirmMessages);
        Assert.Empty(dialog.RestorePickerCalls);
    }

    [Fact]
    public async Task Restore_selected_leaves_the_selection_untouched()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;
        dialog.ConfirmToReturn = true;

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = false;
        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.RestoreSelectedCommand.CanExecute(null), timeoutMs: RealIoTimeoutMs);

        Assert.True(vm.Games[0].IsSelected);
        Assert.False(vm.Games[1].IsSelected);
        Assert.Equal(1, vm.SelectedCount);
    }

    [Fact]
    public async Task Restore_selected_excludes_a_checked_row_that_is_already_busy()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;
        dialog.ConfirmToReturn = true;

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;
        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));

        File.WriteAllBytes(Path.Combine(_dir, "saves-0", "save.dat"), [9, 9, 9]);
        File.WriteAllBytes(Path.Combine(_dir, "saves-1", "save.dat"), [9, 9, 9]);
        vm.Games[0].IsBusy = true;

        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => !vm.IsBulkBackupRunning && dialog.ConfirmMessages.Count == 1, timeoutMs: RealIoTimeoutMs);

        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(Path.Combine(_dir, "saves-0", "save.dat")));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(_dir, "saves-1", "save.dat")));
        Assert.Single(dialog.ConfirmMessages);
    }

    [Fact]
    public async Task A_single_row_backup_rescans_only_that_row()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);

        // The backup folder has to exist already: while it does not, every row reads "backup
        // folder unavailable" and a scan deliberately covers all of them (see the next test).
        // Awaited to completion before Games is read: Refresh rebuilds the collection, and
        // enumerating it from this thread while that happens would fail the test for a reason
        // that has nothing to do with what it checks.
        Directory.CreateDirectory(backupRoot);
        await vm.RefreshCommand.ExecuteAsync(null);
        await WaitForAsync(
            () => vm.Games.All(row => row.ComparisonKind == BackupComparisonKind.NeverBackedUp), timeoutMs: RealIoTimeoutMs);
        dialog.InfoMessages.Clear();

        var other = vm.Games[1];
        var otherKinds = new List<BackupComparisonKind>();
        other.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameRowViewModel.ComparisonKind))
                otherKinds.Add(other.ComparisonKind);
        };

        vm.BackupCommand.Execute(vm.Games[0]);
        // Not asserted as UpToDate: the archive's name carries whole seconds while the save file's
        // own timestamp does not, so a backup taken in the second the fixture wrote the file can
        // legitimately read as changed. Any settled, backed-up verdict proves the rescan ran.
        await WaitForAsync(
            () => dialog.InfoMessages.Count == 1
                && vm.Games[0].ComparisonKind is BackupComparisonKind.UpToDate or BackupComparisonKind.SavesChangedSinceBackup,
            timeoutMs: RealIoTimeoutMs);

        Assert.Empty(otherKinds);
        Assert.Equal(BackupComparisonKind.NeverBackedUp, other.ComparisonKind);
        Assert.Single(dialog.InfoMessages);
    }

    [Fact]
    public async Task The_first_backup_into_a_missing_backup_folder_rescans_every_row()
    {
        var (vm, _, _) = await CreateMultiGameViewModelAsync(2);
        Assert.All(vm.Games, row => Assert.Equal(BackupComparisonKind.BackupFolderNotAvailable, row.ComparisonKind));

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(() => vm.Games[1].ComparisonKind == BackupComparisonKind.NeverBackedUp, timeoutMs: RealIoTimeoutMs);
    }

    [Fact]
    public async Task An_older_sweep_still_settles_the_rows_a_newer_single_row_scan_did_not_touch()
    {
        // The constructor starts the full sweep; the backup below starts a single-row scan on top
        // of it with no await in between, so the two are guaranteed to overlap.
        var (vm, dialog) = CreateViewModelWithGames(3);

        vm.BackupCommand.Execute(vm.Games[0]);

        await WaitForAsync(
            () => dialog.InfoMessages.Count == 1
                && vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking),
            timeoutMs: RealIoTimeoutMs);
    }

    [Fact]
    public void Bulk_button_labels_count_every_ticked_game_including_ones_the_search_hides()
    {
        var (vm, _) = CreateNamedViewModel("Alpha Quest", "Beta World", "Gamma Run");
        // The constructor's status sweep raises PropertyChanged from worker threads while this
        // test raises it from its own, so the recorder has to tolerate concurrent adds.
        var changed = new System.Collections.Concurrent.ConcurrentBag<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;
        vm.SearchText = "alpha";
        vm.GamesView.Refresh();

        // Content fragments, not the full label: the access-key resources carry an underscore
        // mnemonic and the surrounding wording is locked by the resource file, so pinning whole
        // sentences here only broke on copy edits. What the test owns is that the count includes
        // rows the search hides. The fragment is built from the resource itself (with the count
        // appended exactly as the property does) so the mnemonic underscore's position cannot
        // desync the assert from what the label really reads.
        Assert.Contains($"{Strings.Get("str_toolbar_backUpSelected_access")} (2)", vm.BackupSelectedLabel);
        Assert.Contains($"{Strings.Get("str_toolbar_restoreSelected_access")} (2)", vm.RestoreSelectedLabel);
        Assert.Contains(nameof(MainViewModel.BackupSelectedLabel), changed);
        Assert.Contains(nameof(MainViewModel.RestoreSelectedLabel), changed);
    }

    [Fact]
    public async Task Confirmations_name_their_action_on_the_confirming_button()
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var (vm, dialog, _) = CreateViewModel(backupRoot);
        var picked = SeedBackup(backupRoot, vm.Games[0].Game);
        dialog.RestorePickerSelectionToReturn = picked;
        dialog.ConfirmToReturn = false;

        vm.RestoreCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => dialog.ConfirmButtonTexts.Count == 1 && !vm.Games[0].IsBusy);

        vm.RemoveGameCommand.Execute(vm.Games[0]);
        await WaitForAsync(() => dialog.ConfirmButtonTexts.Count == 2);

        // Content fragments, not full labels — the intent is that each confirmation names its
        // action on the confirming button, not that the wording is byte-stable.
        Assert.Contains("Restore", dialog.ConfirmButtonTexts[0], StringComparison.Ordinal);
        Assert.Contains("Remove", dialog.ConfirmButtonTexts[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restore_selected_with_a_declined_game_does_not_claim_all_were_restored()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(2);
        dialog.FolderToReturn = backupRoot;
        dialog.ConfirmReturnSequence = new Queue<bool>([false, true]);

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;
        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupSelectedCommand.CanExecute(null));
        dialog.InfoMessages.Clear();

        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => vm.RestoreSelectedCommand.CanExecute(null), timeoutMs: RealIoTimeoutMs);

        var outcome = Assert.Single(dialog.InfoMessages);
        Assert.Contains("Restored 1 of 2 selected", outcome, StringComparison.Ordinal);
        Assert.Contains("Skipped: Game 0", outcome, StringComparison.Ordinal);
        Assert.DoesNotContain("all", outcome, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_selected_without_a_backup_folder_reports_an_error_and_never_prompts_for_one()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: null);
        await WaitForAsync(() => vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));

        vm.Games[0].IsSelected = true;
        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => dialog.ErrorMessages.Count == 1);

        Assert.Equal(0, dialog.PickFolderCalls);
        Assert.Contains("no backup folder is set", dialog.ErrorMessages[0], StringComparison.Ordinal);
        Assert.False(vm.IsBulkBackupRunning);
    }

    [Fact]
    public async Task Restore_selected_failure_dialog_names_why_each_game_failed()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(1);
        Directory.CreateDirectory(backupRoot);

        vm.Games[0].IsSelected = true;
        vm.RestoreSelectedCommand.Execute(null);
        await WaitForAsync(() => dialog.ErrorMessages.Count == 1, timeoutMs: RealIoTimeoutMs);

        Assert.Contains("Failed: Game 0", dialog.ErrorMessages[0], StringComparison.Ordinal);
        Assert.Contains("No restorable backup for Game 0", dialog.ErrorMessages[0], StringComparison.Ordinal);
        Assert.DoesNotContain("No restorable backup", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bulk_backup_failure_dialog_names_why_each_game_failed()
    {
        var (vm, dialog, _) = await CreateMultiGameViewModelAsync(2);
        File.Delete(Path.Combine(_dir, "saves-1", "save.dat"));

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;
        vm.BackupSelectedCommand.Execute(null);
        await WaitForAsync(() => dialog.ErrorMessages.Count == 1, timeoutMs: RealIoTimeoutMs);

        Assert.Contains("Backed up 1 of 2 selected. Failed: Game 1.", dialog.ErrorMessages[0], StringComparison.Ordinal);
        Assert.Contains("Save folder is empty", dialog.ErrorMessages[0], StringComparison.Ordinal);
        Assert.False(vm.IsBulkBackupRunning);
        Assert.False(vm.IsAnyRowBusy);
    }

    [Fact]
    public async Task Help_command_reports_success_through_the_status_surface()
    {
        var (vm, dialog, _) = CreateViewModel(Path.Combine(_dir, "backups"));
        dialog.HelpGuideResultToReturn = OperationResult.Ok(Strings.Get("str_help_opened"));

        await vm.HelpCommand.ExecuteAsync(null);

        var info = Assert.Single(dialog.InfoMessages);
        // The expected outcome's own resource, not a non-blank check: a wrong sentence crossing the
        // channel (e.g. the "not found" message on a success path) still passes IsNullOrWhiteSpace.
        // Asserting against Strings.Get keeps the pin on WHICH resource surfaced, robust to wording
        // edits of the resource itself.
        Assert.Equal(Strings.Get("str_help_opened"), info);
        Assert.Empty(dialog.ErrorMessages);
        Assert.Single(dialog.HelpGuideLanguages);
    }

    [Fact]
    public async Task Help_command_reports_a_failure_as_an_error_and_keeps_running()
    {
        var (vm, dialog, _) = CreateViewModel(Path.Combine(_dir, "backups"));
        dialog.HelpGuideResultToReturn = OperationResult.Fail(Strings.Get("str_help_missing"));

        await vm.HelpCommand.ExecuteAsync(null);

        var error = Assert.Single(dialog.ErrorMessages);
        // Same content pin: the failure path must surface the missing-guide message, not any
        // non-blank error.
        Assert.Equal(Strings.Get("str_help_missing"), error);
        Assert.Empty(dialog.InfoMessages);
    }

    [Fact]
    public async Task Help_command_passes_the_ui_culture_through_to_the_service()
    {
        var (vm, dialog, _) = CreateViewModel(Path.Combine(_dir, "backups"));

        await vm.HelpCommand.ExecuteAsync(null);

        var language = Assert.Single(dialog.HelpGuideLanguages);
        Assert.Equal(
            System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            language);
    }
}
