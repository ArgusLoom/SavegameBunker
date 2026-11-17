using System.IO;

namespace SavegameBunker.Services;

/// <summary>
/// Classifies an Explorer <c>FileDrop</c> payload for folder drag-and-drop without touching WPF, so the
/// "exactly one existing directory" rule can be unit-tested. The window's DragOver/Drop handlers
/// are the only callers.
/// </summary>
/// <remarks>
/// A <c>.lnk</c> shortcut is a file, so <see cref="Directory.Exists"/> is false for it and the classifier
/// ignores the drop without resolving the target. Multiple items, a missing path, and a null or
/// empty array are the same ignore. Never throws — a path that cannot be inspected is not a
/// directory.
/// </remarks>
public static class DropFolder
{
    /// <summary>
    /// True only when <paramref name="paths"/> holds exactly one existing directory.
    /// <paramref name="directory"/> is that path on true, otherwise <c>""</c>.
    /// </summary>
    public static bool TrySingleExistingDirectory(string[]? paths, out string directory)
    {
        directory = "";
        if (paths is not { Length: 1 })
            return false;

        var path = paths[0];
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            if (!Directory.Exists(path))
                return false;
        }
        catch
        {
            return false;
        }

        directory = path;
        return true;
    }
}
