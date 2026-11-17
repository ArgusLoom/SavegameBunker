using System.IO;

namespace SavegameBunker.Services;

/// <summary>
/// Resolves the offline user guide's <c>index.html</c> for the app's UI language. The release zip
/// ships the built documentation site as <c>help/de/</c> and <c>help/en/</c> beside the executable;
/// the app only reads the folder — it never builds the site. German UI opens the German site,
/// everything else (including English) opens the English one, with a fallback to English when the
/// preferred folder is not in the zip. Both missing means the guide is not installed: the caller
/// reports that instead of starting anything.
/// </summary>
public static class HelpPath
{
    /// <summary>The folder the release zip is expected to carry the guide's English site in.</summary>
    public const string FallbackLanguage = "en";

    /// <summary>
    /// Picks the guide language for a UI culture's two-letter code. Only German is authored
    /// specially — the bare code and every regional variant ("de-AT", "de-DE", "de-LI") read the
    /// German site; every other culture — including unfamiliar two-letter codes — reads English.
    /// A null, empty or whitespace-only value also reads English rather than throwing.
    /// </summary>
    public static string LanguageFor(string? uiLanguage)
    {
        // Real callers hand through Culture.TwoLetterISOLanguageName, but tests exercise the
        // contract directly with regional codes — so normalize first instead of trusting the
        // caller to have trimmed or pre-null-checked.
        var normalized = uiLanguage?.Trim();
        if (string.IsNullOrEmpty(normalized))
            return FallbackLanguage;

        return string.Equals(normalized, "de", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("de-", StringComparison.OrdinalIgnoreCase)
                ? "de"
                : FallbackLanguage;
    }

    /// <summary>
    /// Returns the absolute path of the guide's <c>index.html</c> for <paramref name="uiLanguage"/>,
    /// or null when neither the preferred nor the English site is present. Never throws — an
    /// unreadable path counts as absent, which the caller reports rather than opening.
    /// </summary>
    public static string? Resolve(string baseDirectory, string uiLanguage)
    {
        return ResolveLanguage(baseDirectory, LanguageFor(uiLanguage));

        static string? ResolveLanguage(string baseDirectory, string language)
        {
            var index = Path.Combine(baseDirectory, "help", language, "index.html");
            if (Exists(index))
                return index;

            // The preferred site is missing but English was not the ask — fall back to English
            // once. English missing is the end of the line: the guide is not installed.
            return language == FallbackLanguage
                ? null
                : ResolveLanguage(baseDirectory, FallbackLanguage);
        }

        static bool Exists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
