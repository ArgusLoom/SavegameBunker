using System.IO;

namespace SavegameBunker.Services;

/// <summary>
/// The one place paths are compared for containment — three diverging copies
/// (<c>BackupService</c>'s own normaliser, <c>DirectoryWalk</c>'s prune check and
/// <c>PathResolver.ToTemplate</c>'s trim) were the drive-root bug (ITEM-4): a normalized drive
/// root keeps its trailing separator, and appending a second one to it meant nothing was ever
/// "inside" a drive root.
/// </summary>
/// <remarks>
/// Ordinal, case-insensitive comparison, matching how the Windows file system itself treats drive
/// letters and path segments. Works on a file path too — <see cref="IsSameOrInside"/> only ever
/// compares normalized string prefixes, so pointing it at an archive file instead of a folder is
/// equally valid. Internal: new Services helpers stay off <c>ServiceContractTests</c>' public
/// surface.
/// </remarks>
internal static class PathCompare
{
    /// <summary>
    /// Full path with any trailing separator trimmed — except a drive root (<c>E:\</c>), which
    /// keeps its separator: trimming it would turn <c>E:\</c> into <c>E:</c>, the current directory
    /// on that drive rather than its root. A UNC share root (<c>\\server\share\</c>) is trimmed
    /// like any other path, because <see cref="Path.GetPathRoot(string)"/> reports it without the
    /// separator; <see cref="IsSameOrInside"/> appends one back before comparing.
    /// </summary>
    internal static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        return Path.EndsInDirectorySeparator(full) && full.Length == Path.GetPathRoot(full)?.Length
            ? full
            : Path.TrimEndingDirectorySeparator(full);
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> is <paramref name="parent"/> itself or sits anywhere
    /// beneath it. A separator is appended to the normalized parent only when it does not already
    /// end with one — a root stays a root after <see cref="Normalize"/>, and appending a second
    /// separator to it is exactly what made a drive-root parent never contain anything.
    /// </summary>
    internal static bool IsSameOrInside(string candidate, string parent)
    {
        var child = Normalize(candidate);
        var root = Normalize(parent);
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return child.Equals(root, StringComparison.OrdinalIgnoreCase)
            || child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
