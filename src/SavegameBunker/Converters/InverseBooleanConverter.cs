using System.Globalization;
using System.Windows.Data;

namespace SavegameBunker.Converters;

/// <summary>
/// Inverts a boolean, for the bindings that need "enabled while NOT busy".
/// </summary>
/// <remarks>
/// Introduced after the alternative — expressing the same thing as a <c>DataTrigger</c> inside a
/// property-element <c>Style</c> — silently broke the row overflow button: replacing its
/// <c>Style</c> attribute with a <c>Style</c> element cost it the icon chrome, leaving a control
/// too small to hit, and Restore lives behind that button alone. One converter class is the
/// cheaper thing to own.
/// </remarks>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}
