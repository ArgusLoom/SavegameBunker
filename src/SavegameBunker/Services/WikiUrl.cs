namespace SavegameBunker.Services;

/// <summary>
/// Builds and allowlists the PCGamingWiki search URL. Kept out of
/// <see cref="ShellOpen"/> so that allowlist stays json/directory only.
/// </summary>
public static class WikiUrl
{
    public const string HostPrefix = "https://www.pcgamingwiki.com/";

    /// <summary>
    /// True only for https URLs on www.pcgamingwiki.com (OrdinalIgnoreCase). Null, blank, http,
    /// and any other host are refused so <c>Process.Start</c> cannot be pointed at an exe.
    /// </summary>
    public static bool IsAllowed(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith(HostPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Search URL for the typed display name. Caller must not invoke this with blank input —
    /// the command's CanExecute already refuses that.
    /// </summary>
    public static string Search(string displayName) =>
        HostPrefix + "w/index.php?search=" + Uri.EscapeDataString(displayName.Trim());
}
