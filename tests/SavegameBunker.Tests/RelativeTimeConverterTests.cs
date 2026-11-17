using System.Globalization;
using SavegameBunker.Converters;

namespace SavegameBunker.Tests;

/// <summary>
/// Exercises every threshold boundary and both guard branches of the ported converter:
/// a null value, a non-<see cref="DateTime"/> value, and a future
/// timestamp must all render as readable text, never a negative number or a blank cell.
/// </summary>
public sealed class RelativeTimeConverterTests
{
    private static readonly RelativeTimeConverter Converter = new();

    private static string Convert(object? value) =>
        (string)Converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture)!;

    [Fact]
    public void Null_value_renders_as_an_empty_string()
    {
        Assert.Equal("", Convert(null));
    }

    [Fact]
    public void A_non_date_value_renders_as_an_empty_string()
    {
        Assert.Equal("", Convert("not a date"));
    }

    [Fact]
    public void Just_under_a_minute_ago_reads_just_now()
    {
        Assert.Equal("just now", Convert(DateTime.Now.AddSeconds(-30)));
    }

    [Fact]
    public void A_few_minutes_ago_reads_minutes_ago()
    {
        Assert.Equal("5 minutes ago", Convert(DateTime.Now.AddMinutes(-5)));
    }

    [Fact]
    public void A_few_hours_ago_reads_hours_ago()
    {
        Assert.Equal("3 hours ago", Convert(DateTime.Now.AddHours(-3)));
    }

    [Fact]
    public void A_few_days_ago_reads_days_ago()
    {
        Assert.Equal("5 days ago", Convert(DateTime.Now.AddDays(-5)));
    }

    [Fact]
    public void Beyond_thirty_days_falls_back_to_the_absolute_date()
    {
        var value = DateTime.Now.AddDays(-45);
        Assert.Equal(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Convert(value));
    }

    [Fact]
    public void A_future_timestamp_renders_as_readable_text_not_a_negative_number()
    {
        var value = DateTime.Now.AddHours(2);
        var result = Convert(value);

        Assert.DoesNotContain("minutes ago", result);
        Assert.DoesNotMatch(@"^-", result);
        Assert.Equal(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), result);
    }

    [Fact]
    public void ConvertBack_is_not_supported()
    {
        Assert.Throws<NotSupportedException>(
            () => Converter.ConvertBack(null, typeof(DateTime?), null, CultureInfo.InvariantCulture));
    }
}
