using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Exercises the restore picker's header wording, row ordering and selection state without a
/// running window — mirrors <c>ScanWindowViewModelTests</c>'s no-WPF-window shape.
/// </summary>
public sealed class RestorePickerViewModelTests
{
    private static BackupArchive Archive(string fileName, DateTime takenAt, long sizeBytes = 1024) =>
        new($"C:/backups/test-game/{fileName}", takenAt, sizeBytes);

    [Fact]
    public void One_archive_produces_a_singular_header()
    {
        var vm = new RestorePickerViewModel("Test Game", [Archive("a.zip", new DateTime(2026, 1, 1))]);

        // Content fragments, not full sentences — the resource file locks the wording; the test
        // owns the singular form for one archive.
        Assert.Contains("1 backup", vm.HeaderText);
        Assert.DoesNotContain("backups", vm.HeaderText);
    }

    [Fact]
    public void Two_or_more_archives_produce_a_plural_header_naming_the_count()
    {
        var vm = new RestorePickerViewModel("Test Game",
        [
            Archive("a.zip", new DateTime(2026, 1, 2)),
            Archive("b.zip", new DateTime(2026, 1, 1)),
        ]);

        // Same content-fragment rule: plural noun and the count, not the whole sentence.
        Assert.Contains("2 backups", vm.HeaderText);
        Assert.Contains("newest first", vm.HeaderText);
    }

    [Fact]
    public void Rows_appear_in_the_order_supplied_with_no_re_sorting()
    {
        var older = Archive("older.zip", new DateTime(2026, 1, 1));
        var newer = Archive("newer.zip", new DateTime(2026, 1, 5));

        // Deliberately supplied oldest-first — the view model must not impose its own sort.
        var vm = new RestorePickerViewModel("Test Game", [older, newer]);

        Assert.Equal([older, newer], vm.Rows.Select(r => r.Archive));
    }

    [Fact]
    public void Two_archives_with_an_identical_TakenAt_produce_two_distinct_rows()
    {
        var takenAt = new DateTime(2026, 3, 15, 10, 0, 0);
        var first = Archive("first.zip", takenAt);
        var second = Archive("second.zip", takenAt);

        var vm = new RestorePickerViewModel("Test Game", [first, second]);

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal([first, second], vm.Rows.Select(r => r.Archive));
    }

    [Fact]
    public void Row_exposes_the_raw_timestamp_and_delegates_size_to_SizeFormat()
    {
        var takenAt = new DateTime(2026, 7, 4, 9, 5, 0);
        var archive = Archive("a.zip", takenAt, sizeBytes: 12345);
        var vm = new RestorePickerViewModel("Test Game", [archive]);

        var row = Assert.Single(vm.Rows);
        Assert.Equal(takenAt, row.TakenAt);
        Assert.Equal(SizeFormat.Describe(12345), row.SizeText);
    }

    [Fact]
    public void An_ordinary_archives_file_name_reports_it_is_not_a_safety_backup()
    {
        var archive = Archive("2026-07-04-090500.zip", new DateTime(2026, 7, 4));
        var vm = new RestorePickerViewModel("Test Game", [archive]);

        Assert.False(Assert.Single(vm.Rows).IsSafetyBackup);
    }

    [Fact]
    public void A_pre_restore_suffixed_archives_file_name_reports_it_is_a_safety_backup()
    {
        var archive = Archive("2026-07-04-090500-pre-restore.zip", new DateTime(2026, 7, 4));
        var vm = new RestorePickerViewModel("Test Game", [archive]);

        Assert.True(Assert.Single(vm.Rows).IsSafetyBackup);
    }

    [Fact]
    public void CanRestore_is_false_before_any_row_is_selected_and_true_after_one_is()
    {
        var archive = Archive("a.zip", new DateTime(2026, 1, 1));
        var vm = new RestorePickerViewModel("Test Game", [archive]);

        Assert.False(vm.CanRestore);

        vm.SelectedBackup = vm.Rows.Single();

        Assert.True(vm.CanRestore);
    }

    [Fact]
    public void Title_names_the_game()
    {
        var vm = new RestorePickerViewModel("Elden Ring", []);

        // Content fragment: the test owns that the title names the game, not the exact caption.
        Assert.Contains("Elden Ring", vm.Title);
    }

    /// <summary>
    /// The delete buttons' IsEnabled bindings requery only on <c>CanExecuteChanged</c>, so
    /// <see cref="RestorePickerViewModel.IsDeletePending"/> changing must notify the command —
    /// both transitions. Without it, while one row's delete is pending the other rows' buttons
    /// render enabled and their clicks die silently on the <c>CanDeleteArchive</c> guard.
    /// Driven directly through the property so the assertion is deterministic: going through
    /// <c>DeleteArchiveCommand.ExecuteAsync</c> would mix in the command's own start/stop
    /// notifications, which also raise <c>CanExecuteChanged</c> and would mask a missing one here.
    /// </summary>
    [Fact]
    public void Toggling_IsDeletePending_notifies_DeleteArchiveCommand_on_both_transitions()
    {
        var dialogs = new RecordingDialogService();
        var vm = new RestorePickerViewModel(
            "Test Game", [Archive("a.zip", new DateTime(2026, 1, 1))],
            dialogs, path => new BackupService().DeleteArchive(path));
        var row = vm.Rows.Single();

        var canExecuteAtRaise = new List<bool>();
        vm.DeleteArchiveCommand.CanExecuteChanged += (_, _) =>
            canExecuteAtRaise.Add(vm.DeleteArchiveCommand.CanExecute(row));

        vm.IsDeletePending = true;
        Assert.Contains(false, canExecuteAtRaise);

        vm.IsDeletePending = false;
        Assert.True(canExecuteAtRaise[^1]);
    }
}

/// <summary>
/// Exercises the restore picker's per-row delete flow against a real temp directory, using the
/// shared <see cref="RecordingDialogService"/> fake: exactly the
/// confirmed file goes, a declined confirmation deletes nothing, a failed delete leaves the row
/// in place and surfaces an error, and the list and header stay consistent afterwards.
/// </summary>
public sealed class RestorePickerDeleteTests : IDisposable
{
    private readonly string _dir;

    public RestorePickerDeleteTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    private BackupArchive RealArchive(string fileName)
    {
        var path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, [1, 2, 3]);
        return new BackupArchive(path, new DateTime(2026, 1, 1), 3);
    }

    [Fact]
    public async Task Confirming_a_delete_removes_exactly_that_one_file_and_updates_the_list()
    {
        var keep = RealArchive("keep.zip");
        var gone = RealArchive("gone.zip");
        var dialogs = new RecordingDialogService { ConfirmToReturn = true };
        var service = new BackupService();
        var vm = new RestorePickerViewModel(
            "Test Game", [gone, keep], dialogs, path => service.DeleteArchive(path));
        var row = vm.Rows.Single(r => r.Archive == gone);

        await vm.DeleteArchiveCommand.ExecuteAsync(row);

        Assert.False(File.Exists(gone.FilePath));
        Assert.True(File.Exists(keep.FilePath));
        Assert.DoesNotContain(row, vm.Rows);
        Assert.Single(vm.Rows);
        Assert.Contains("1 backup", vm.HeaderText);
    }

    [Fact]
    public async Task A_declined_confirmation_deletes_nothing()
    {
        var archive = RealArchive("a.zip");
        var dialogs = new RecordingDialogService { ConfirmToReturn = false };
        var service = new BackupService();
        var vm = new RestorePickerViewModel(
            "Test Game", [archive], dialogs, path => service.DeleteArchive(path));

        await vm.DeleteArchiveCommand.ExecuteAsync(vm.Rows.Single());

        Assert.True(File.Exists(archive.FilePath));
        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task A_failed_delete_leaves_the_row_and_reports_an_error()
    {
        var archive = RealArchive("locked.zip");
        var dialogs = new RecordingDialogService { ConfirmToReturn = true };
        var vm = new RestorePickerViewModel(
            "Test Game", [archive], dialogs,
            path => OperationResult.Fail("The archive could not be deleted — it may be open in another program."));

        await vm.DeleteArchiveCommand.ExecuteAsync(vm.Rows.Single());

        Assert.True(File.Exists(archive.FilePath));
        Assert.Single(vm.Rows);
        var error = Assert.Single(dialogs.ErrorMessages);
        Assert.Contains("could not be deleted", error);
    }

    [Fact]
    public async Task Deleting_the_selected_row_clears_the_selection()
    {
        var archive = RealArchive("a.zip");
        var dialogs = new RecordingDialogService { ConfirmToReturn = true };
        var service = new BackupService();
        var vm = new RestorePickerViewModel(
            "Test Game", [archive], dialogs, path => service.DeleteArchive(path));
        vm.SelectedBackup = vm.Rows.Single();
        Assert.True(vm.CanRestore);

        await vm.DeleteArchiveCommand.ExecuteAsync(vm.Rows.Single());

        Assert.Null(vm.SelectedBackup);
        Assert.False(vm.CanRestore);
        Assert.Empty(vm.Rows);
        Assert.Contains("0 backups", vm.HeaderText);
    }

    [Fact]
    public async Task The_delete_flow_asks_for_confirmation_before_touching_the_disk()
    {
        var archive = RealArchive("a.zip");
        var dialogs = new RecordingDialogService { ConfirmToReturn = true };
        var service = new BackupService();
        var vm = new RestorePickerViewModel(
            "Test Game", [archive], dialogs, path => service.DeleteArchive(path));

        await vm.DeleteArchiveCommand.ExecuteAsync(vm.Rows.Single());

        Assert.Single(dialogs.ConfirmMessages);
    }

    [Fact]
    public async Task A_locked_archive_fails_to_delete_without_crashing()
    {
        var archive = RealArchive("locked.zip");
        // The fixture really wrote the file. A lock test against a missing file is
        // indistinguishable from a lock test against a locked one — both fail the delete —
        // so the flow must start from a proven-existing file.
        Assert.True(File.Exists(archive.FilePath));
        var dialogs = new RecordingDialogService { ConfirmToReturn = true };
        var service = new BackupService();

        // Hold the file open with no FileShare across BOTH the direct service call and the
        // view-model delete, exactly as another program would hold the zip: the service must
        // return a failure, not throw, and the lock must still be in place when the VM runs.
        // (A lock released before the VM step would let that delete succeed and the file
        // vanish — the failure this assertion pair exists to catch.)
        using (File.Open(archive.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = service.DeleteArchive(archive.FilePath);
            Assert.False(result.Success); // direct proof the lock alone blocks the delete

            // The same failure channel through the view model: row stays, error is surfaced.
            var vm = new RestorePickerViewModel(
                "Test Game", [archive], dialogs,
                path => new BackupService().DeleteArchive(path));
            await vm.DeleteArchiveCommand.ExecuteAsync(vm.Rows.Single());

            Assert.True(File.Exists(archive.FilePath));
            Assert.Single(vm.Rows);
            Assert.Single(dialogs.ErrorMessages);
        }
    }

    [Fact]
    public async Task An_archive_removed_since_it_was_listed_fails_instead_of_reporting_a_success()
    {
        var archive = new BackupArchive(Path.Combine(_dir, "vanished.zip"), new DateTime(2026, 1, 1), 0);
        var dialogs = new RecordingDialogService { ConfirmToReturn = true };
        var vm = new RestorePickerViewModel(
            "Test Game", [archive], dialogs, path => new BackupService().DeleteArchive(path));

        await vm.DeleteArchiveCommand.ExecuteAsync(vm.Rows.Single());

        var error = Assert.Single(dialogs.ErrorMessages);
        Assert.Contains("not found", error);
        Assert.Single(vm.Rows);
    }

    /// <summary>
    /// A delete blocked inside its own <c>deleteArchive</c> step must disable Restore and every
    /// other delete button for its whole pending window, and free them again afterwards — the
    /// same behavior a restore racing the delete or a second delete clicking through would break.
    /// </summary>
    [Fact]
    public async Task A_pending_delete_disables_restore_and_further_deletes_until_it_settles()
    {
        var first = RealArchive("first.zip");
        var second = RealArchive("second.zip");
        var dialogs = new RecordingDialogService { ConfirmToReturn = true };
        var gate = new TaskCompletionSource();
        var vm = new RestorePickerViewModel(
            "Test Game", [first, second], dialogs,
            // Blocks inside the delete step until the test releases it — that is exactly the
            // pending window whose guards this fact observes.
            _ => { gate.Task.Wait(); return OperationResult.Ok("deleted"); });
        var row1 = vm.Rows[0];
        var row2 = vm.Rows[1];
        vm.SelectedBackup = row2;
        Assert.True(vm.CanRestore);

        var delete = vm.DeleteArchiveCommand.ExecuteAsync(row1);
        await WaitForPendingDelete(vm);

        // Mid-delete: Restore is disabled even though a row is still selected, and no second
        // delete button may fire.
        Assert.True(vm.IsDeletePending);
        Assert.False(vm.CanRestore);
        Assert.False(vm.DeleteArchiveCommand.CanExecute(row2));

        gate.TrySetResult();
        await delete;
        await WaitForPendingDelete(vm, expectPending: false);

        // Settled (success path): everything is usable again, the row is gone and the selection
        // on the untouched row survived the block.
        Assert.False(vm.IsDeletePending);
        Assert.True(vm.CanRestore);
        Assert.True(vm.DeleteArchiveCommand.CanExecute(row2));
        Assert.DoesNotContain(row1, vm.Rows);
        Assert.Equal(row2, vm.SelectedBackup);
    }

    [Fact]
    public async Task A_failed_delete_frees_restore_and_delete_again()
    {
        var archive = RealArchive("a.zip");
        var dialogs = new RecordingDialogService { ConfirmToReturn = true };
        var vm = new RestorePickerViewModel(
            "Test Game", [archive], dialogs,
            _ => OperationResult.Fail("The archive could not be deleted."));
        vm.SelectedBackup = vm.Rows.Single();
        Assert.True(vm.CanRestore);

        await vm.DeleteArchiveCommand.ExecuteAsync(vm.Rows.Single());

        // The finally reset covers the error exit: nothing stays disabled after a failed delete.
        Assert.False(vm.IsDeletePending);
        Assert.True(vm.CanRestore);
        Assert.True(vm.DeleteArchiveCommand.CanExecute(vm.Rows.Single()));
        Assert.Single(dialogs.ErrorMessages);
    }

    private static async Task WaitForPendingDelete(RestorePickerViewModel vm, bool expectPending = true)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(3000);
        while (vm.IsDeletePending != expectPending && DateTime.UtcNow < deadline)
            await Task.Delay(5);
        Assert.Equal(expectPending, vm.IsDeletePending);
    }
}
