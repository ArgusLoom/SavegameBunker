using System.Globalization;

namespace SavegameBunker.Services;

/// <summary>
/// The one byte-count-to-human-readable rule, shared by the Add Game dialog's resolve-and-report
/// line and the main window's Size column so both describe a given byte total
/// identically.
/// </summary>
public static class SizeFormat
{
    private const long Kb = 1024;
    private const long Mb = Kb * 1024;
    private const long Gb = Mb * 1024;

    /// <summary>
    /// Below 1 MB reports KB, below 1 GB reports MB, at or above 1 GB reports GB — always one
    /// decimal place, rounded away from zero, formatted with the invariant culture so the reported
    /// size does not change with the machine's locale.
    /// </summary>
    public static string Describe(long bytes)
    {
        var (value, unit) = bytes switch
        {
            >= Gb => (bytes / (double)Gb, "GB"),
            >= Mb => (bytes / (double)Mb, "MB"),
            _ => (bytes / (double)Kb, "KB"),
        };

        var rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        return $"{rounded.ToString("0.0", CultureInfo.InvariantCulture)} {unit}";
    }
}
