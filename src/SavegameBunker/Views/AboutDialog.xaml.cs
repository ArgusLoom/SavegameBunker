using SavegameBunker.ViewModels;
using Wpf.Ui.Controls;

namespace SavegameBunker.Views;

public partial class AboutDialog : FluentWindow
{
    public AboutDialog(AboutDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
