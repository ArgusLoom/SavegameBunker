using SavegameBunker.ViewModels;
using Wpf.Ui.Controls;

namespace SavegameBunker.Views;

/// <summary>
/// The About window: version, build date and the user guide link. Modal over the main window, so
/// it carries its own dialog host — a help-link error drawn in the main window's host would sit
/// under it, out of reach (same as for the restore picker).
/// </summary>
public sealed partial class AboutDialog : FluentWindow
{
    /// <summary>
    /// Takes no view model: <see cref="Services.DialogService"/> needs this window's
    /// <c>RootDialogHost</c> before it can build the About-owned dialog service the view model
    /// reports into, so it constructs the window first and assigns <c>DataContext</c> itself.
    /// </summary>
    public AboutDialog()
    {
        InitializeComponent();
    }
}
