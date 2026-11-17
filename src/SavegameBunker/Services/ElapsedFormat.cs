using System.Globalization;

namespace SavegameBunker.Services;

/// <summary>
/// The one elapsed-time wording rule, shared by the Verdict column's "restoring now would undo
/// about …" sentence and the restore confirmation's newer-saves warning — two call
/// sites, one rule, so the two sentences can never disagree about how long ago something happened.
/// </summary>
public static class ElapsedFormat
{
    /// <summary>
    /// Renders the single largest whole unit, never a combination — this app does not combine
    /// units anywhere else. Truncates rather than rounds, so 119 minutes is "1 hour", not "2
    /// hours". A negative or zero span falls into the below-one-minute branch: an inverted
    /// comparison across machines is an accepted limitation and must produce readable text,
    /// never a negative number.
    /// </summary>
    public static string Describe(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes < 1)
            return "less than a minute";

        if (elapsed.TotalHours < 1)
        {
            var minutes = (int)elapsed.TotalMinutes;
            return minutes == 1 ? "1 minute" : $"{minutes.ToString(CultureInfo.InvariantCulture)} minutes";
        }

        if (elapsed.TotalDays < 1)
        {
            var hours = (int)elapsed.TotalHours;
            return hours == 1 ? "1 hour" : $"{hours.ToString(CultureInfo.InvariantCulture)} hours";
        }

        var days = (int)elapsed.TotalDays;
        return days == 1 ? "1 day" : $"{days.ToString(CultureInfo.InvariantCulture)} days";
    }
}
