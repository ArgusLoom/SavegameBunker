namespace SavegameBunker.Services;

/// <summary>
/// Ids name a folder under the backup root. This class is the one place that rule lives: both
/// the catalog loader (validating a hand-edited id) and the Add Game dialog (deriving one from a
/// display name) call it, so a hand-edited catalog is held to the exact rule the dialog enforces.
/// </summary>
public static class IdSlug
{
    /// <summary>
    /// Windows' reserved device names (case-insensitive, with or without an extension) — a folder
    /// by one of these names cannot be created or addressed normally through Explorer or most Win32
    /// APIs. The id is already constrained to lowercase letters, digits and hyphens by the time this
    /// is checked, so a plain lowercase set is enough; no case-folding is needed here.
    /// </summary>
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.Ordinal)
    {
        "con", "prn", "aux", "nul",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    };

    /// <summary>
    /// True when the id is non-empty, every character is a lowercase letter, a digit or a hyphen,
    /// and it is not one of Windows' reserved device names. Nothing outside that character set can
    /// appear, so an id can never contain a path separator or a traversal segment. An empty id is
    /// never safe — it names no folder at all. A reserved device name is excluded separately: it
    /// passes the character check (letters and digits only) but names a folder Explorer and most
    /// Win32 APIs cannot address normally, whatever backupRoot it sits under.
    /// </summary>
    public static bool IsSafe(string id)
    {
        if (id.Length == 0)
            return false;

        foreach (var c in id)
        {
            var ok = c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '-';
            if (!ok)
                return false;
        }

        return !ReservedDeviceNames.Contains(id);
    }

    /// <summary>
    /// Derives a candidate id from a display name: lowercase, every character outside a-z/0-9
    /// becomes a hyphen, runs of hyphens collapse to one, and leading/trailing hyphens are
    /// trimmed. Non-ASCII letters are not transliterated — they map to '-' like any other
    /// character the safety rule excludes. An input with no matching characters returns "".
    /// </summary>
    public static string Derive(string displayName)
    {
        var lowered = displayName.ToLowerInvariant();
        var mapped = new char[lowered.Length];
        for (var i = 0; i < lowered.Length; i++)
        {
            var c = lowered[i];
            mapped[i] = c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-';
        }

        var collapsed = new System.Text.StringBuilder(mapped.Length);
        var lastWasHyphen = false;
        foreach (var c in mapped)
        {
            if (c == '-')
            {
                if (lastWasHyphen)
                    continue;
                lastWasHyphen = true;
            }
            else
            {
                lastWasHyphen = false;
            }

            collapsed.Append(c);
        }

        return collapsed.ToString().Trim('-');
    }
}
