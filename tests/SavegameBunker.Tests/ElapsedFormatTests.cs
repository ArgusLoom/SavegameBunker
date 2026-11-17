using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// One assertion per boundary and one step either side of the seven-row elapsed-time table.
/// </summary>
public sealed class ElapsedFormatTests
{
    [Fact]
    public void Zero_renders_less_than_a_minute() =>
        Assert.Equal("less than a minute", ElapsedFormat.Describe(TimeSpan.Zero));

    [Fact]
    public void Fiftynine_seconds_renders_less_than_a_minute() =>
        Assert.Equal("less than a minute", ElapsedFormat.Describe(TimeSpan.FromSeconds(59)));

    [Fact]
    public void Exactly_sixty_seconds_renders_1_minute() =>
        Assert.Equal("1 minute", ElapsedFormat.Describe(TimeSpan.FromSeconds(60)));

    [Fact]
    public void Onehundrednineteen_seconds_renders_1_minute() =>
        Assert.Equal("1 minute", ElapsedFormat.Describe(TimeSpan.FromSeconds(119)));

    [Fact]
    public void Exactly_two_minutes_renders_2_minutes() =>
        Assert.Equal("2 minutes", ElapsedFormat.Describe(TimeSpan.FromMinutes(2)));

    [Fact]
    public void Fiftynine_minutes_renders_59_minutes() =>
        Assert.Equal("59 minutes", ElapsedFormat.Describe(TimeSpan.FromMinutes(59)));

    [Fact]
    public void Exactly_sixty_minutes_renders_1_hour() =>
        Assert.Equal("1 hour", ElapsedFormat.Describe(TimeSpan.FromMinutes(60)));

    [Fact]
    public void Onehundrednineteen_minutes_renders_1_hour() =>
        Assert.Equal("1 hour", ElapsedFormat.Describe(TimeSpan.FromMinutes(119)));

    [Fact]
    public void Exactly_two_hours_renders_2_hours() =>
        Assert.Equal("2 hours", ElapsedFormat.Describe(TimeSpan.FromHours(2)));

    [Fact]
    public void Twentythree_hours_fiftynine_minutes_renders_23_hours() =>
        Assert.Equal("23 hours", ElapsedFormat.Describe(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59)));

    [Fact]
    public void Exactly_twentyfour_hours_renders_1_day() =>
        Assert.Equal("1 day", ElapsedFormat.Describe(TimeSpan.FromHours(24)));

    [Fact]
    public void Fortyseven_hours_fiftynine_minutes_renders_1_day() =>
        Assert.Equal("1 day", ElapsedFormat.Describe(TimeSpan.FromHours(47) + TimeSpan.FromMinutes(59)));

    [Fact]
    public void Exactly_fortyeight_hours_renders_2_days() =>
        Assert.Equal("2 days", ElapsedFormat.Describe(TimeSpan.FromHours(48)));

    [Fact]
    public void Thirty_days_renders_30_days() =>
        Assert.Equal("30 days", ElapsedFormat.Describe(TimeSpan.FromDays(30)));

    [Fact]
    public void Negative_timespan_renders_less_than_a_minute_with_no_minus_sign()
    {
        var text = ElapsedFormat.Describe(TimeSpan.FromMinutes(-5));

        Assert.Equal("less than a minute", text);
        Assert.DoesNotContain("-", text);
    }
}
