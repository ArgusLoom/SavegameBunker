using Wpf.Ui.Controls;

namespace SavegameBunker.Views;

/// <summary>
/// The restore picker: modal over the main window, so it carries its own dialog host for the
/// confirmations and errors it raises (G-02-6).
/// </summary>
public sealed partial class RestorePickerWindow : FluentWindow
{
    /// <summary>
    /// Takes no view model, unlike the sibling dialogs. <see cref="Services.DialogService"/> needs
    /// this window's <c>RootDialogHost</c> to exist before it can build the picker-owned dialog
    /// service the view model's delete confirmation renders into (G-02-6) — so it constructs this
    /// window first, builds that service, then assigns <c>DataContext</c> itself.
    /// </summary>
    public RestorePickerWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Refuses to close while a delete confirmation is open in this window's own host. The view
    /// model's background thread waits on that dialog; closing the window would take the dialog
    /// with it and leave the wait, and the delete, hanging for the rest of the session.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is ViewModels.RestorePickerViewModel { IsDeletePending: true })
            e.Cancel = true;
        base.OnClosing(e);
    }
}
