using System.IO;

namespace SavegameBunker.Services;

/// <summary>
/// Classifies a path before <c>Process.Start(..., UseShellExecute = true)</c>. Shell-execute on an
/// <c>.exe</c> / <c>.bat</c> / shortcut — or any of the many other extensions Windows Explorer
/// knows how to launch or invoke a handler for — is how a hand-edited catalog or a command-line
/// path that failed to load as JSON would otherwise run whatever the path pointed at when the user
/// clicked Open.
/// <para>
/// An allowlist rather than a denylist of dangerous extensions: <see cref="Classify"/> has exactly
/// two callers, one always opening the catalog file (which is always <c>.json</c>) and one always
/// opening a save folder (always a directory) — so allowing precisely those two shapes refuses
/// everything else by construction, including whatever Explorer shell handler a denylist has not
/// been updated to name yet.
/// </para>
/// </summary>
public static class ShellOpen
{
    public enum Kind
    {
        Missing,
        Directory,
        File,
        UnsafeFile,
    }

    /// <summary>
    /// Directory vs <c>.json</c> file vs "this would run code". Never throws — a path that cannot
    /// be inspected is treated as missing, which the caller reports rather than launching.
    /// </summary>
    public static Kind Classify(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Kind.Missing;

        try
        {
            if (Directory.Exists(path))
                return Kind.Directory;

            if (!File.Exists(path))
                return Kind.Missing;

            return Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? Kind.File
                : Kind.UnsafeFile;
        }
        catch
        {
            return Kind.Missing;
        }
    }
}
