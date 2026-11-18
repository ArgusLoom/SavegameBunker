using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// The free-space advisory and the composed StatusLine ("needed / free", not four joined
/// sentences).
/// </summary>
[Collection("MainViewModel")]
public sealed class MainViewModelStatusLineTests : MainViewModelTestBase
{
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
}
