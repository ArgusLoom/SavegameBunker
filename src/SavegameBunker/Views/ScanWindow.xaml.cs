using SavegameBunker.ViewModels;
using Wpf.Ui.Controls;

namespace SavegameBunker.Views;

public partial class ScanWindow : FluentWindow
{
    public ScanWindow(ScanWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
