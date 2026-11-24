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
}
