using System.Windows;
using System.Windows.Controls.Primitives;

namespace SavegameBunker.Behaviors;

/// <summary>
/// Lets a confirm button close its dialog from XAML with no code-behind handler. Set
/// <c>b:DialogResultBehavior.DialogResult="True"</c> on a button; clicking it assigns that value
/// to the owning window's own <see cref="Window.DialogResult"/>, closing the window exactly as a
/// code-behind handler doing the same assignment would.
/// </summary>
/// <remarks>
/// Only valid on a button inside a window shown with <see cref="Window.ShowDialog()"/> — assigning
/// <see cref="Window.DialogResult"/> on a modeless window throws. <c>bool?</c>, not <c>bool</c>,
/// because that is <see cref="Window.DialogResult"/>'s own type: a non-null default would make an
/// explicitly-set <c>false</c> indistinguishable from "never set" and silently do nothing.
/// <c>Microsoft.Xaml.Behaviors.Wpf</c> was considered and declined for this — a whole dependency
/// for three lines of code-behind in a project whose defining zero-package constraint had already
/// been spent twice.
/// </remarks>
public static class DialogResultBehavior
{
    public static readonly DependencyProperty DialogResultProperty = DependencyProperty.RegisterAttached(
        "DialogResult", typeof(bool?), typeof(DialogResultBehavior), new PropertyMetadata(null, OnDialogResultChanged));

    public static bool? GetDialogResult(ButtonBase button) => (bool?)button.GetValue(DialogResultProperty);

    public static void SetDialogResult(ButtonBase button, bool? value) => button.SetValue(DialogResultProperty, value);

    private static void OnDialogResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (ButtonBase)d;
        button.Click -= OnClick; // Idempotent: a re-set does not double-subscribe.
        if (e.NewValue is not null)
            button.Click += OnClick;
    }

    private static void OnClick(object sender, RoutedEventArgs e)
    {
        var button = (ButtonBase)sender;
        if (Window.GetWindow(button) is { } window)
            window.DialogResult = GetDialogResult(button);
    }
}
