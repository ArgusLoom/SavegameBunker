using SavegameBunker.ViewModels;
using Wpf.Ui.Controls;

namespace SavegameBunker.Views;

public partial class RestorePickerWindow : FluentWindow
{
    public RestorePickerWindow(RestorePickerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
