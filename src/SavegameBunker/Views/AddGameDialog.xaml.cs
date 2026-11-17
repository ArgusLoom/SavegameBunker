using System.Windows.Input;
using SavegameBunker.ViewModels;
using Wpf.Ui.Controls;

namespace SavegameBunker.Views;

public partial class AddGameDialog : FluentWindow
{
    public AddGameDialog(AddGameDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private AddGameDialogViewModel ViewModel => (AddGameDialogViewModel)DataContext;

    /// <summary>
    /// Forwards a suggestion click to the view model's commit command. Makes no decision
    /// of its own: it does not read the list's selection, name a suggestion type or call the apply
    /// method — the click already updated <see cref="AddGameDialogViewModel.SelectedSuggestion"/>
    /// through the list's two-way binding before this handler runs, and the command itself is a
    /// no-op when nothing is highlighted. Survives as the one piece of event plumbing WPF offers
    /// no declarative event-to-command binding for, short of a behaviours package, which was declined.
    /// </summary>
    private void SuggestionsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ViewModel.CommitSuggestionCommand.Execute(null);
        e.Handled = true;
    }
}
