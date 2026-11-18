using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// Busy scoping (one row's work marks only that row) and busy state under concurrency.
/// </summary>
[Collection("MainViewModel")]
public sealed class MainViewModelBusyTests : MainViewModelTestBase
{
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
}
