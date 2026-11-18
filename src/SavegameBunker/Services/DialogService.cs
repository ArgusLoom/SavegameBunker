using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using SavegameBunker.Models;
using SavegameBunker.ViewModels;
using SavegameBunker.Views;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace SavegameBunker.Services;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/>.
/// </summary>
/// <remarks>
/// Every call marshals onto the UI thread. Services run off it, so calls such as
/// <see cref="PickFolder"/> reach this class from a background thread — touching a Window from
/// there would throw. Doing it here keeps the marshalling in the one WPF-aware class instead of
/// leaking a Dispatcher into the view models.
/// <para>
/// The three message surfaces are all backed here: <see cref="Info"/> shows a transient
/// <c>Snackbar</c> via <see cref="ISnackbarService"/>; <see cref="Error"/> and <see cref="Confirm"/>
/// show a blocking <c>ContentDialog</c> via <see cref="IContentDialogService"/>. Neither
/// <see cref="IDialogService"/>'s signatures nor its callers changed — only what backs them did.
/// </para>
/// </remarks>
public sealed class DialogService : IDialogService
{
    private static readonly TimeSpan SnackbarTimeout = TimeSpan.FromSeconds(4);

    private readonly Window _owner;
    private readonly IContentDialogService _contentDialogService;
    private readonly ISnackbarService _snackbarService;

    public DialogService(Window owner, IContentDialogService contentDialogService, ISnackbarService snackbarService)
    {
        _owner = owner;
        _contentDialogService = contentDialogService;
        _snackbarService = snackbarService;
    }

    public void Info(string message)
    {
        // Fire-and-forget on both branches: Info returns void and the snackbar is transient, so
        // nothing downstream waits on it — only where the call is marshalled from differs.
        if (_owner.Dispatcher.CheckAccess())
            _snackbarService.Show("SavegameBunker", message, ControlAppearance.Success, null, SnackbarTimeout);
        else
            _owner.Dispatcher.InvokeAsync(() => _snackbarService.Show("SavegameBunker", message, ControlAppearance.Success, null, SnackbarTimeout));
    }

    public void Error(string message)
    {
        var options = ErrorDialogOptions(message);

        // Fire-and-forget on both branches, matching Info: Error returns void, so the caller is
        // never blocked on the dialog closing — only where the call is marshalled from differs.
        // The discard is deliberate (see ShowQueuedDiscardingFaults), not a bare `_ =`.
        if (_owner.Dispatcher.CheckAccess())
            ShowQueuedDiscardingFaults(options);
        else
            _owner.Dispatcher.InvokeAsync(() => ShowQueuedDiscardingFaults(options));
    }

    /// <summary>
    /// Shows a queued error dialog and observes its failure deliberately instead of leaving the
    /// task unobserved. A faulted <c>ShowSimpleDialogAsync</c> left unobserved would surface as an
    /// App-domain unobserved-task exception, whose own error report queues another dialog — a
    /// GC-timed feedback loop. The fault is swallowed rather than reported because Error has no
    /// return channel and re-reporting a failed error dialog cannot help the user; the next
    /// dialog queued behind it still proceeds, since <see cref="ShowAfterAsync"/> awaits the
    /// previous task inside its own try/catch.
    /// </summary>
    private void ShowQueuedDiscardingFaults(SimpleContentDialogCreateOptions options)
    {
        _ = ShowQueuedAsync(options).ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>The most recently queued content dialog; touched on the UI thread only.</summary>
    private Task _lastContentDialog = Task.CompletedTask;

    /// <summary>
    /// Shows a content dialog once every dialog queued before it has closed. The window has one
    /// dialog host, and two operations can each want it at the same moment — a backup failing
    /// while another row's restore confirmation is still open. Showing the second on top of the
    /// first would leave <see cref="Confirm"/>'s background caller waiting on an answer the user
    /// can no longer give, with its row busy and the window unclosable; queueing keeps every
    /// dialog answerable in turn without depending on what the host does with an overlap.
    /// Must be called on the UI thread.
    /// </summary>
    private Task<ContentDialogResult> ShowQueuedAsync(SimpleContentDialogCreateOptions options)
    {
        var shown = ShowAfterAsync(_lastContentDialog, options);
        _lastContentDialog = shown;
        return shown;
    }

    private async Task<ContentDialogResult> ShowAfterAsync(Task previous, SimpleContentDialogCreateOptions options)
    {
        try
        {
            await previous;
        }
        catch
        {
            // A dialog that failed to show must not block every dialog queued behind it.
        }

        return await _contentDialogService.ShowSimpleDialogAsync(options, CancellationToken.None);
    }

    public bool Confirm(string message, string confirmButtonText)
    {
        var options = ConfirmDialogOptions(message, confirmButtonText);

        // Callers reach Confirm from a background thread (under Task.Run) — every caller wraps it
        // in Task.Run (MainViewModel.PromptUnsecuredGamesOnce, MainViewModel.RemoveGameAsync,
        // GameActions' restore, RestorePickerViewModel.DeleteArchiveAsync). That caller must block
        // until the user answers, so the dialog is started with InvokeAsync (queues onto the UI
        // thread's own dispatcher without blocking it) and the *unwrapped* task is awaited
        // synchronously from this — the calling background — thread.
        // Blocking the UI thread itself here (e.g. via Dispatcher.Invoke running the wait inline)
        // would deadlock: the dialog needs that thread's own message pump to resolve, and a
        // synchronous wait on it stops that pump from running.
        Debug.Assert(!_owner.Dispatcher.CheckAccess(),
            "Confirm blocks its caller until the user answers; call it off the UI thread (Task.Run), never on it.");

        var task = _owner.Dispatcher.InvokeAsync(() => ShowQueuedAsync(options)).Task.Unwrap();
        return task.GetAwaiter().GetResult() == ContentDialogResult.Primary;
    }

    private static SimpleContentDialogCreateOptions ErrorDialogOptions(string message) => new()
    {
        Title = "SavegameBunker",
        Content = message,
        CloseButtonText = "OK",
    };

    private static SimpleContentDialogCreateOptions ConfirmDialogOptions(string message, string confirmButtonText) => new()
    {
        Title = "SavegameBunker",
        Content = message,
        PrimaryButtonText = confirmButtonText,
        CloseButtonText = Strings.Get("str_common_cancel"),
    };

    public string? PickFolder(string title, string? initialDirectory) =>
        OnUiThread(() =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = title,
                InitialDirectory = initialDirectory
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };

            return dialog.ShowDialog(_owner) == true ? dialog.FolderName : null;
        });

    public AddGameResult? ShowAddGameDialog(IReadOnlyCollection<string> existingIds, string? initialSaveFolder = null) =>
        OnUiThread(() =>
        {
            var vm = new AddGameDialogViewModel(existingIds, this);
            if (!string.IsNullOrWhiteSpace(initialSaveFolder))
                vm.SaveFolder = initialSaveFolder;
            var dialog = new AddGameDialog(vm) { Owner = _owner };
            return dialog.ShowDialog() == true ? vm.BuildResult() : null;
        });

    public IReadOnlyList<KnownGame>? ShowFindGamesDialog(ScanResult result) =>
        OnUiThread(() =>
        {
            var vm = new ScanWindowViewModel(result);
            var dialog = new ScanWindow(vm) { Owner = _owner };
            return dialog.ShowDialog() == true ? vm.BuildSelection() : null;
        });

    public BackupArchive? ShowRestorePickerDialog(string gameDisplayName, IReadOnlyList<BackupArchive> backups,
        Func<string, OperationResult> deleteArchive) =>
        OnUiThread(() =>
        {
            var vm = new RestorePickerViewModel(gameDisplayName, backups, this, deleteArchive);
            var dialog = new RestorePickerWindow(vm) { Owner = _owner };
            return dialog.ShowDialog() == true ? vm.SelectedBackup?.Archive : null;
        });

    public string? ShowChangeSaveFolderDialog(string gameDisplayName, string currentTemplate) =>
        OnUiThread(() =>
        {
            var vm = new ChangeSaveFolderDialogViewModel(gameDisplayName, currentTemplate, this);
            var dialog = new ChangeSaveFolderDialog(vm) { Owner = _owner };
            return dialog.ShowDialog() == true ? vm.SaveFolder : null;
        });

    /// <summary>
    /// Unlike every other member here, this creates no <see cref="Window"/> — starting a shell
    /// process needs no owner and touches no WPF object — so it does not go through
    /// <see cref="OnUiThread{T}"/>. It is still safe to call from a background thread.
    /// </summary>
    public OperationResult OpenInEditor(string path)
    {
        switch (ShellOpen.Classify(path))
        {
            case ShellOpen.Kind.Missing:
                return OperationResult.Fail(Strings.Get("str_open_notFound"));
            case ShellOpen.Kind.UnsafeFile:
                return OperationResult.Fail(Strings.Get("str_open_refused"));
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return OperationResult.Ok(Strings.Format("str_open_opened", path));
        }
        catch (Win32Exception)
        {
            // No application associated with .json — fall back to Explorer with the file selected.
            try
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
                return OperationResult.Ok(Strings.Format("str_open_openedInExplorer", path));
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(Strings.Format("str_open_failed", path, ex.Message));
            }
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(Strings.Format("str_open_failed", path, ex.Message));
        }
    }

    /// <summary>
    /// Like <see cref="OpenInEditor"/>, this creates no <see cref="Window"/> and is safe from a
    /// background thread. The path is resolved from <see cref="AppContext.BaseDirectory"/> — never
    /// from user input — so it goes straight to the shell without <see cref="ShellOpen"/>'s
    /// allowlist, which exists for catalog- and user-supplied paths and allows only `.json` files
    /// and directories.
    /// </summary>
    public OperationResult OpenHelpGuide(string uiLanguage)
    {
        var index = HelpPath.Resolve(AppContext.BaseDirectory, uiLanguage);
        if (index is null)
            return OperationResult.Fail(Strings.Get("str_help_missing"));

        try
        {
            Process.Start(new ProcessStartInfo(index) { UseShellExecute = true });
            return OperationResult.Ok(Strings.Get("str_help_opened"));
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(Strings.Format("str_help_failed", ex.Message));
        }
    }

    public OperationResult OpenHttps(string url)
    {
        if (!WikiUrl.IsAllowed(url))
            return OperationResult.Fail(Strings.Get("str_openHttps_refused"));

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return OperationResult.Ok(Strings.Get("str_openHttps_opened"));
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(Strings.Format("str_openHttps_failed", ex.Message));
        }
    }

    private T OnUiThread<T>(Func<T> action) =>
        _owner.Dispatcher.CheckAccess() ? action() : _owner.Dispatcher.Invoke(action);
}
