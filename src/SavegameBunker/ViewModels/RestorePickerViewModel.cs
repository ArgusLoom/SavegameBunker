using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SavegameBunker.Models;
using SavegameBunker.Services;
using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace SavegameBunker.ViewModels;

/// <summary>
/// Backs the restore picker window: one row per archive, newest-first, and the single selection the
/// user is choosing to restore from. Each row also carries
/// <see cref="RestorePickerRowViewModel.IsSafetyBackup"/>, so a safety archive is never
/// indistinguishable from an ordinary one.
/// </summary>
/// <remarks>
/// Modelled on <see cref="ScanWindowViewModel"/> but simplified to single selection: no
/// checkboxes, no Select All/None. Rows are added in the order the <c>backups</c> argument arrives
/// in and never re-sorted — <see cref="BackupService.ListBackups"/> already guarantees newest-first,
/// so a second sort here would be redundant, hand-rolled duplication.
/// <para>
/// Derives from <see cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject"/> via the
/// <c>CommunityToolkitObservableObject</c> alias every view model in this namespace uses.
/// </para>
/// </remarks>
public sealed partial class RestorePickerViewModel : CommunityToolkitObservableObject
{
    /// <summary>
    /// Deletes one archive's ZIP file, as provided by <see cref="DialogService"/> (which fronts
    /// <see cref="BackupService.DeleteArchive"/>). Optional so the picker stays constructible
    /// without delete support; <see cref="DeleteArchiveAsync"/> is a no-op without one.
    /// </summary>
    private readonly Func<string, OperationResult>? _deleteArchive;

    /// <summary>The confirmation and error dialogs the delete flow raises.</summary>
    private readonly IDialogService? _dialogs;

    /// <param name="dialogs">
    /// The dialog service the delete confirmation and any delete error go through. Optional;
    /// when absent the per-row delete button has no backing flow and never fires.
    /// </param>
    /// <param name="deleteArchive">
    /// Deletes the ZIP file at a given path, returning an <see cref="OperationResult"/> it never
    /// throws across. Optional for the same reason as <paramref name="dialogs"/>.
    /// </param>
    public RestorePickerViewModel(string gameDisplayName, IReadOnlyList<BackupArchive> backups,
        IDialogService? dialogs = null, Func<string, OperationResult>? deleteArchive = null)
    {
        _dialogs = dialogs;
        _deleteArchive = deleteArchive;

        Title = Strings.Format("str_restorePicker_title", gameDisplayName);

        foreach (var backup in backups)
            Rows.Add(new RestorePickerRowViewModel(backup));

        HeaderText = BuildHeaderText(Rows.Count);
    }

    public string Title { get; }

    /// <summary>
    /// Recomputed after a delete, because the one/many wording depends on how many rows are left.
    /// </summary>
    [ObservableProperty]
    public partial string HeaderText { get; set; }

    public ObservableCollection<RestorePickerRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestore))]
    public partial RestorePickerRowViewModel? SelectedBackup { get; set; }

    /// <summary>
    /// Raised while a delete is pending (confirmation + <c>File.Delete</c>) — the same
    /// immediate-set, finally-reset shape <see cref="GameRowViewModel.IsBusy"/> uses in
    /// <c>MainViewModel</c>. While it is up, Restore and every delete button are disabled: a
    /// restore racing the delete could pick the archive currently being removed, and a second
    /// delete could remove two archives off one confirmation's outcome.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestore))]
    public partial bool IsDeletePending { get; set; }

    // Raised on BOTH transitions: without it the delete buttons' IsEnabled bindings only catch up
    // on some unrelated WPF command requery, so while a delete is pending the other rows' buttons
    // render enabled and their clicks die silently on the CanDeleteArchive guard — exactly the
    // gap this notification closes.
    partial void OnIsDeletePendingChanged(bool value) => DeleteArchiveCommand.NotifyCanExecuteChanged();

    public bool CanRestore => SelectedBackup is not null && !IsDeletePending;

    private static string BuildHeaderText(int count) => count == 1
        ? Strings.Get("str_restorePicker_headerOne")
        : Strings.Format("str_restorePicker_headerMany", count);

    /// <summary>
    /// Deletes this one row's archive after a confirmation — the only feature in the picker that
    /// removes anything from disk, and deliberately scoped to the single confirmed ZIP file: never
    /// the backup root, never a batch. Confirm runs off the UI thread because
    /// <see cref="IDialogService.Confirm"/> blocks on a dispatcher-marshalled dialog and would
    /// deadlock if waited on here (the same constraint RemoveGameAsync and RestoreService already
    /// honour). A declined confirmation deletes nothing; a failed delete leaves the row in place
    /// and surfaces the service's message as an error dialog.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteArchive))]
    private async Task DeleteArchiveAsync(RestorePickerRowViewModel? row)
    {
        if (row is null || _dialogs is null || _deleteArchive is null || IsDeletePending)
            return;

        IsDeletePending = true;
        try
        {
            var confirmed = await Task.Run(() => _dialogs.Confirm(
                Strings.Get("str_restorePicker_deleteConfirm"),
                Strings.Get("str_restorePicker_deleteConfirmButton")));
            if (!confirmed)
                return;

            var result = await Task.Run(() => _deleteArchive(row.Archive.FilePath));
            if (!result.Success)
            {
                _dialogs.Error(result.Message);
                return;
            }

            // A deleted archive can no longer be restored from — drop it from the list and clear the
            // selection if the deleted row was the one selected.
            if (SelectedBackup == row)
                SelectedBackup = null;
            Rows.Remove(row);
            HeaderText = BuildHeaderText(Rows.Count);
        }
        finally
        {
            // Cleared on every exit path, including a failed delete and a declined confirmation.
            IsDeletePending = false;
        }
    }

    private bool CanDeleteArchive(RestorePickerRowViewModel? row) =>
        row is not null && _dialogs is not null && _deleteArchive is not null && !IsDeletePending;
}

/// <summary>One row in the restore picker — wraps a single <see cref="BackupArchive"/>.</summary>
public sealed class RestorePickerRowViewModel
{
    public RestorePickerRowViewModel(BackupArchive archive)
    {
        Archive = archive;
    }

    public BackupArchive Archive { get; }

    /// <summary>
    /// The archive's raw timestamp, replacing the pre-formatted <c>TakenAtText</c> this
    /// row used to carry. Bound through the ported <c>RelativeTimeConverter</c> for the visible text,
    /// converter-less for the row's absolute-value <c>ToolTip</c>.
    /// </summary>
    public DateTime TakenAt => Archive.TakenAt;

    /// <summary>
    /// True when this archive is one of restore's own safety backups (the
    /// <c>-pre-restore</c> file-name suffix <see cref="BackupService"/> writes), so a safety
    /// archive is never visually indistinguishable from an ordinary one.
    /// </summary>
    public bool IsSafetyBackup => Archive.IsSafetyBackup;

    public string SizeText => SizeFormat.Describe(Archive.SizeBytes);
}
