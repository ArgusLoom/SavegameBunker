using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// "Back up selected" and "Restore selected": checkbox-driven bulk backup and restore.
/// </summary>
[Collection("MainViewModel")]
public sealed class MainViewModelBulkTests : MainViewModelTestBase
{
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
    public async Task Bulk_backup_success_dialog_carries_each_games_incompleteness_warning()
    {
        var (vm, dialog, _) = await CreateMultiGameViewModelAsync(2);
        File.WriteAllText(Path.Combine(_dir, "saves-0", "other.dat"), "readable");

        vm.Games[0].IsSelected = true;
        vm.Games[1].IsSelected = true;
        using (new FileStream(Path.Combine(_dir, "saves-0", "save.dat"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.BackupSelectedCommand.Execute(null);
            await WaitForAsync(() => dialog.InfoMessages.Count == 1, timeoutMs: RealIoTimeoutMs);
        }

        // The status line stays the one-line summary; the dialog says what the archive lacks.
        var info = Assert.Single(dialog.InfoMessages);
        Assert.StartsWith("Backed up all 2 selected games.", info, StringComparison.Ordinal);
        Assert.Contains("NOT in this backup", info, StringComparison.Ordinal);
        Assert.Contains("save.dat", info, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT in this backup", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void About_command_opens_the_about_dialog()
    {
        var (vm, dialog, _) = CreateViewModel(Path.Combine(_dir, "backups"));

        vm.AboutCommand.Execute(null);

        Assert.Equal(1, dialog.ShowAboutDialogCalls);
    }
}
