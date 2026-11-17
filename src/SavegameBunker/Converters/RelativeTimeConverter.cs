using System.Globalization;
using System.Windows.Data;
using SavegameBunker.Services;

namespace SavegameBunker.Converters;

/// <summary>
/// Renders a nullable <see cref="DateTime"/> as elapsed time ("2 hours ago"), for binding
/// alongside a second, converter-less binding to the same property that carries the exact
/// absolute value as a <c>ToolTip</c> (the "relative, exact on hover" rule). Registered
/// once in <c>App.xaml</c> under the key <c>RelTime</c>.
/// </summary>
/// <remarks>
/// Adapted from an earlier relative-time converter,
/// keeping the threshold ladder and the future-timestamp guard exactly as they are,
/// with two changes: the type check targets SavegameBunker's nullable <see cref="DateTime"/> rather
/// than the offset-aware type the original used (a type check against the
/// wrong type fails silently, rendering every relative-time cell blank with no binding error),
/// and the phrases come from <see cref="Strings"/> rather than the original's localization service,
/// which SavegameBunker does not have.
/// </remarks>
[ValueConversion(typeof(DateTime?), typeof(string))]
public sealed class RelativeTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt)
            return string.Empty;

        var span = DateTime.Now - dt;

        // A negative span — clock skew, or an archive carried between machines — must render as
        // readable text, never a negative number.
        if (span < TimeSpan.Zero)
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Singular has its own authored sentence at each step: "1 minutes ago" is the kind of
        // detail that makes an application feel unfinished, and it showed up on the first real
        // screen this was ever looked at.
        if (span.TotalSeconds < 60) return Strings.Get("str_relTime_justNow");
        if (span.TotalMinutes < 60) return Plural((int)span.TotalMinutes, "minute");
        if (span.TotalHours < 24) return Plural((int)span.TotalHours, "hour");
        if (span.TotalDays < 30) return Plural((int)span.TotalDays, "day");
        return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>
    /// Picks the singular or plural authored sentence for <paramref name="unit"/>. Both are
    /// authored in Strings.en.xaml; neither is composed here.
    /// </summary>
    private static string Plural(int value, string unit) =>
        value == 1
            ? Strings.Get($"str_relTime_{unit}Ago")
            : Strings.Format($"str_relTime_{unit}sAgo", value);
}
