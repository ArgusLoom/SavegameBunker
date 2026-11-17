using System.Windows;
using System.Windows.Media;

namespace SavegameBunker.Behaviors;

/// <summary>
/// Clips an element to a rounded rectangle, so a child that draws its own square corners cannot
/// poke out of a rounded container.
/// </summary>
/// <remarks>
/// A <see cref="System.Windows.Controls.Border"/>'s <c>CornerRadius</c> rounds the border's own
/// stroke and background but does not clip what sits inside it: a DataGrid's column-header strip
/// keeps its square corners and overlaps the curve, which is what made the header read as sharper
/// than everything around it. WPF has no "clip to my corner radius" property, and the alternative —
/// a SizeChanged handler in the window's code-behind — is exactly the code-behind the views are kept
/// free of, so the geometry is maintained here instead and applied by attaching a radius.
/// </remarks>
public static class CornerClip
{
    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.RegisterAttached(
            "Radius",
            typeof(double),
            typeof(CornerClip),
            new PropertyMetadata(0d, OnRadiusChanged));

    public static void SetRadius(DependencyObject element, double value) =>
        element.SetValue(RadiusProperty, value);

    public static double GetRadius(DependencyObject element) =>
        (double)element.GetValue(RadiusProperty);

    private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
            return;

        // Re-cut on every size change: the geometry is in the element's own coordinates, so a
        // resized window would otherwise keep clipping to its old bounds.
        element.SizeChanged -= Recut;
        element.SizeChanged += Recut;
        Apply(element, (double)e.NewValue);
    }

    private static void Recut(object sender, SizeChangedEventArgs e) =>
        Apply((FrameworkElement)sender, GetRadius((DependencyObject)sender));

    private static void Apply(FrameworkElement element, double radius)
    {
        if (radius <= 0 || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            element.Clip = null;
            return;
        }

        element.Clip = new RectangleGeometry(
            new Rect(0, 0, element.ActualWidth, element.ActualHeight), radius, radius);
    }
}
