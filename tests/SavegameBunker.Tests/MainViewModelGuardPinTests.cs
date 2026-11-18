using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Characterizes the guard semantics the MainViewModel split (REF-01/REF-03) must preserve: the
/// bulk-run claim order (reset-to-Checking before any row is claimed), the mid-run re-entrancy
/// state, bulk restore's game-by-game confirmation order, and the zero-row paths for every bulk
/// command and Refresh. These facts pin today's behavior against unchanged production code and
/// are frozen from the moment this plan lands — Plans 02-05 never edit this file, exactly like
/// <see cref="MainViewModelTests"/>.
/// </summary>
public sealed class MainViewModelGuardPinTests : IDisposable
{
    private readonly string _dir;

    public MainViewModelGuardPinTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            TestFileSystem.RemoveDenyRules(_dir);
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
        catch (UnauthorizedAccessException)
        {
            // A Deny ACE that could not be lifted must not fail the whole run.
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        Assert.True(condition(), "Timed out waiting for the expected view model state.");
    }

    /// <summary>
    /// Copied verbatim from <see cref="MainViewModelTests"/>'s own private fixture of the same
    /// name — that file is read, never edited, by this plan.
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
    public async Task Bulk_backup_resets_targets_to_checking_and_raises_the_counters_before_claiming_any_row()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;

        foreach (var row in vm.Games)
            row.ApplyBackupComparison(BackupComparison.NeverBackedUp());

        var captured = false;
        BackupComparisonKind[]? capturedKinds = null;
        var capturedIsAnyRowChecking = false;
        var capturedTotal = -1;
        string? capturedCurrentGameName = null;

        // The catalog's backup root is already configured (CreateMultiGameViewModelAsync sets
        // it), so EnsureBackupRootAsync returns without awaiting anything — the whole claim
        // (reset every target to Checking, raise the counters, then set IsBusy on every target
        // in order) runs synchronously inside Execute, before any await yields control. This
        // handler fires inside the first target's own IsBusy setter, on the command's own
        // thread, strictly after the reset and the counters above it in RunBulkBackupAsync —
        // not a timing assumption.
        vm.Games[0].PropertyChanged += (_, e) =>
        {
            if (captured || e.PropertyName != nameof(GameRowViewModel.IsBusy) || !vm.Games[0].IsBusy)
                return;

            captured = true;
            capturedKinds = [.. vm.Games.Select(r => r.ComparisonKind)];
            capturedIsAnyRowChecking = vm.IsAnyRowChecking;
            capturedTotal = vm.BulkBackupTotal;
            capturedCurrentGameName = vm.BulkBackupCurrentGameName;
        };

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null), timeoutMs: 15000);

        Assert.True(captured);
        Assert.All(capturedKinds!, kind => Assert.Equal(BackupComparisonKind.Checking, kind));
        Assert.True(capturedIsAnyRowChecking);
        Assert.Equal(3, capturedTotal);
        Assert.Equal("Game 0", capturedCurrentGameName);
    }

    [Fact]
    public async Task Mid_bulk_backup_every_target_is_checking_and_busy_and_nothing_else_can_start()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        dialog.FolderToReturn = backupRoot;

        // Rows 0 and 1 are the bulk-backup targets; row 2 is up to date, so it is outside the
        // run — ticking it proves the bulk-run terms, not the empty-selection term, are what
        // blocks BackupSelected/RestoreSelected below.
        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[1].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.Games[2].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[2].IsSelected = true;

        var captured = false;
        var capturedKinds = Array.Empty<BackupComparisonKind>();
        var capturedBusy = Array.Empty<bool>();
        var capturedIsAnyRowChecking = false;
        var capturedIsBulkBackupRunning = false;
        var capturedCanExecute = Array.Empty<bool>();

        // Row 1 is the second (last) target claimed by the reset-then-IsBusy sequence, so its own
        // IsBusy flip to true is the deterministic point at which both targets are already
        // Checking and busy — not a timing assumption.
        vm.Games[1].PropertyChanged += (_, e) =>
        {
            if (captured || e.PropertyName != nameof(GameRowViewModel.IsBusy) || !vm.Games[1].IsBusy)
                return;

            captured = true;
            capturedKinds = [vm.Games[0].ComparisonKind, vm.Games[1].ComparisonKind];
            capturedBusy = [vm.Games[0].IsBusy, vm.Games[1].IsBusy];
            capturedIsAnyRowChecking = vm.IsAnyRowChecking;
            capturedIsBulkBackupRunning = vm.IsBulkBackupRunning;
            capturedCanExecute =
            [
                vm.BackupSelectedCommand.CanExecute(null),
                vm.RestoreSelectedCommand.CanExecute(null),
                vm.RefreshCommand.CanExecute(null),
                vm.ChangeBackupRootCommand.CanExecute(null),
                vm.BackupCommand.CanExecute(vm.Games[2]),
                vm.RestoreCommand.CanExecute(vm.Games[2]),
            ];
        };

        vm.BackupAllChangedCommand.Execute(null);
        await WaitForAsync(() => vm.BackupAllChangedCommand.CanExecute(null), timeoutMs: 15000);

        Assert.True(captured);
        Assert.All(capturedKinds, kind => Assert.Equal(BackupComparisonKind.Checking, kind));
        Assert.All(capturedBusy, busy => Assert.True(busy));
        Assert.True(capturedIsAnyRowChecking);
        Assert.True(capturedIsBulkBackupRunning);
        Assert.All(capturedCanExecute, canExecute => Assert.False(canExecute));
    }

    [Fact]
    public async Task Bulk_restore_resets_targets_and_switches_to_the_restore_sentence_before_claiming_any_row()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;

        var captured = false;
        var capturedKinds = Array.Empty<BackupComparisonKind>();
        var capturedTotal = -1;
        string? capturedProgressText = null;

        vm.Games[0].PropertyChanged += (_, e) =>
        {
            if (captured || e.PropertyName != nameof(GameRowViewModel.IsBusy) || !vm.Games[0].IsBusy)
                return;

            captured = true;
            capturedKinds = [vm.Games[0].ComparisonKind, vm.Games[1].ComparisonKind];
            capturedTotal = vm.BulkBackupTotal;
            capturedProgressText = vm.BulkBackupProgressText;
        };

        vm.RestoreSelectedCommand.Execute(null);

        // No archives exist for either game, so the run fails both and reports exactly once —
        // the shared outcome-report call, not one dialog per failed game.
        await WaitForAsync(() => !vm.IsBulkBackupRunning && dialog.ErrorMessages.Count == 1, timeoutMs: 15000);

        Assert.True(captured);
        Assert.All(capturedKinds, kind => Assert.Equal(BackupComparisonKind.Checking, kind));
        Assert.Equal(2, capturedTotal);
        Assert.Equal(Strings.Format("str_bulkRestore_progress", 0, 2, "Game 0"), capturedProgressText);
    }

    [Fact]
    public async Task Bulk_restore_confirms_each_game_in_Games_order()
    {
        var (vm, dialog, backupRoot) = await CreateMultiGameViewModelAsync(3);
        foreach (var row in vm.Games)
        {
            var result = new BackupService().Backup(row.Game, backupRoot);
            Assert.True(result.Success, result.Message);
        }

        foreach (var row in vm.Games)
            row.IsSelected = true;

        // Every game is declined, so no save folder is ever swapped.
        dialog.ConfirmToReturn = false;

        await vm.RestoreSelectedCommand.ExecuteAsync(null);
        await WaitForAsync(() => !vm.IsBulkBackupRunning);

        Assert.Equal(3, dialog.ConfirmMessages.Count);
        for (var i = 0; i < 3; i++)
            Assert.Contains($"Game {i}", dialog.ConfirmMessages[i]);
    }

    [Fact]
    public async Task Bulk_commands_on_an_empty_catalog_do_nothing_and_never_prompt()
    {
        // The unset backup root means any reach into the backup-folder picker would be visible.
        var load = new CatalogLoad(
            Path.Combine(_dir, "games.json"),
            new Catalog { BackupRoot = null, Games = [] },
            [], false, null);
        var dialog = new RecordingDialogService();
        var vm = new MainViewModel(load, dialog);

        var statusBefore = vm.StatusMessage;

        await vm.BackupAllChangedCommand.ExecuteAsync(null);
        await vm.BackupSelectedCommand.ExecuteAsync(null);
        await vm.RestoreSelectedCommand.ExecuteAsync(null);

        Assert.Equal(0, dialog.PickFolderCalls);
        Assert.Empty(dialog.InfoMessages);
        Assert.Empty(dialog.ErrorMessages);
        Assert.Empty(dialog.ConfirmMessages);
        Assert.Equal(0, vm.BulkBackupTotal);
        Assert.False(vm.IsAnyRowChecking);
        Assert.Equal(statusBefore, vm.StatusMessage);
    }

    [Fact]
    public async Task A_refresh_of_an_empty_catalog_settles_with_no_row_checking_and_the_load_message()
    {
        var catalogPath = Path.Combine(_dir, "games.json");
        await GameCatalog.SaveAsync(catalogPath, new Catalog { BackupRoot = null, Games = [] });
        var load = await GameCatalog.LoadAsync(catalogPath);

        var dialog = new RecordingDialogService();
        var vm = new MainViewModel(load, dialog);
        vm.RequestStartupUnsecuredPrompt();

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.Games);
        Assert.False(vm.IsAnyRowChecking);
        Assert.Equal(Strings.Get("str_load_empty"), vm.StatusMessage);
        Assert.Empty(dialog.ConfirmMessages);
    }
}
