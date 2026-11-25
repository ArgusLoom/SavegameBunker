using System.ComponentModel;
using System.Windows;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using Wpf.Ui.Controls;

namespace SavegameBunker;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // Windows High Contrast makes Light/Dark not a choice — the toggle hides entirely
        // rather than sitting there disabled.
        ThemeToggleButton.Visibility = App.IsHighContrastActive ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Flips the session-only theme (no settings.json — see <see cref="App.ToggleTheme"/>).
    /// The glyph is <c>Icon.Theme</c> (a crescent) in both modes — SavegameBunkerIcons.xaml has
    /// one theme glyph, not a sun/moon pair that would swap with the mode.
    /// </summary>
    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        App.ToggleTheme();
    }

    // The row overflow's ContextMenu (opened as a DropDownButton.Flyout) renders in its own
    // visual-tree root, so RelativeSource/ElementName bindings on its MenuItem.Command never
    // reach the window's DataContext — the command binds to null and clicks are silently inert.
    // Routed Click events still bubble normally, so the commands are invoked directly here instead.
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OpenSaveFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is GameRowViewModel row)
            ViewModel.OpenSaveFolderCommand.Execute(row);
    }

    private void ChangeSaveFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is GameRowViewModel row)
            ViewModel.ChangeSaveFolderCommand.Execute(row);
    }

    private void RemoveGameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is GameRowViewModel row)
            ViewModel.RemoveGameCommand.Execute(row);
    }

    /// <summary>
    /// The selection column's cell <c>CheckBox</c> lives in a <c>DataGridTemplateColumn.CellTemplate</c>
    /// on a grid with <c>IsReadOnly="True"</c> and no <c>CellEditingTemplate</c> — a <c>DataGrid</c>
    /// never authoritatively commits such a control's <c>TwoWay</c> binding back to its source; it only
    /// commits on incidental triggers (cell/row navigation, <c>CellEditEnding</c>) that this grid does
    /// not reliably fire. Without this handler, a click flips only the CheckBox's own visual
    /// <c>IsChecked</c> while <see cref="GameRowViewModel.IsSelected"/> is never written, so the next
    /// <c>GamesView.Refresh()</c> rebuilds the row against the real, still-unset value — the defect
    /// where a row checked under a search filter came back unchecked once the filter
    /// cleared. Forcing the commit from the CheckBox's own <c>Checked</c>/<c>Unchecked</c> events
    /// reliably flushes the pending value.
    ///
    /// The lookup is null-conditional because a container discarded during a <c>GamesView.Refresh()</c>
    /// rebuild can raise this event after its binding expression is already gone; an unguarded call
    /// there would be an unhandled crash on the UI thread.
    /// </summary>
    private void SelectionCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        var checkBox = (System.Windows.Controls.CheckBox)sender;
        checkBox.GetBindingExpression(System.Windows.Controls.CheckBox.IsCheckedProperty)?.UpdateSource();
    }

    /// <summary>
    /// Accept a drop only when FileDrop is exactly one existing directory. Effects
    /// and Handled must be set here or WPF never raises Drop. Highlight
    /// follows the same classify so invalid payloads get no chrome.
    /// </summary>
    private void MainWindow_PreviewDragOver(object sender, DragEventArgs e)
    {
        var ok = DropFolder.TrySingleExistingDirectory(FileDropPaths(e), out _);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        ViewModel.IsDragFolderOver = ok;
    }

    private void MainWindow_PreviewDragLeave(object sender, DragEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (pos.X < 0 || pos.Y < 0 || pos.X > ActualWidth || pos.Y > ActualHeight)
            ViewModel.IsDragFolderOver = false;
    }

    private void MainWindow_PreviewDrop(object sender, DragEventArgs e)
    {
        ViewModel.IsDragFolderOver = false;
        e.Handled = true;
        if (!DropFolder.TrySingleExistingDirectory(FileDropPaths(e), out var directory))
            return;
        _ = ViewModel.AddGameFromDroppedFolderAsync(directory);
    }

    private static string[]? FileDropPaths(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop)
            ? e.Data.GetData(DataFormats.FileDrop) as string[]
            : null;

    /// <summary>
    /// Restore's swap step (safety backup already taken → old save directory moved aside → new one
    /// renamed in) is two sequential renames, not one atomic operation — tearing the process down
    /// between them would leave the save directory missing until the stray sibling is found and
    /// renamed back by hand. Backup is not similarly at risk (it only renames a verified `.tmp` into
    /// place after writing), but is refused here anyway so the user gets one consistent rule rather
    /// than having to remember which of the two operations is safe to interrupt.
    /// </summary>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsAnyBackupOrRestoreRunning)
            return;

        e.Cancel = true;
        ViewModel.NotifyCloseBlockedWhileBusy();
    }
}
