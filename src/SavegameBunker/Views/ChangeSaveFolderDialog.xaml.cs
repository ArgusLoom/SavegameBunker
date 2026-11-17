using SavegameBunker.ViewModels;
using Wpf.Ui.Controls;

namespace SavegameBunker.Views;

/// <summary>
/// The repair route for a broken save path: the same <c>ui:FluentWindow</c> chrome the other three dialogs use, with
/// no code-behind decisions of its own — every choice lives in
/// <see cref="ChangeSaveFolderDialogViewModel"/>.
/// </summary>
public partial class ChangeSaveFolderDialog : FluentWindow
{
    public ChangeSaveFolderDialog(ChangeSaveFolderDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
