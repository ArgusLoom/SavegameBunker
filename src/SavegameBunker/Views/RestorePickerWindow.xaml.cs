using Wpf.Ui.Controls;

namespace SavegameBunker.Views;

public partial class RestorePickerWindow : FluentWindow
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
