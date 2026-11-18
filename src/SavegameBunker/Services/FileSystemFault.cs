using System.IO;

namespace SavegameBunker.Services;

/// <summary>
/// The one predicate for "this exception means a locked, vanished, too-long or permission-denied
/// path" — the fault a filesystem catch-and-skip site needs to tell apart from a genuinely
/// unexpected failure. <see cref="DirectoryNotFoundException"/>, <see cref="FileNotFoundException"/>
/// and <see cref="PathTooLongException"/> all derive from <see cref="IOException"/>, so the
/// per-site lists that used to name them beside <see cref="IOException"/> were the same set
/// spelled differently (ITEM-9) — this predicate is behavior-identical to every list it replaces.
/// </summary>
internal static class FileSystemFault
{
    /// <summary>True when <paramref name="ex"/> is a filesystem fault a path-walking site should
    /// catch and skip, rather than let propagate as unexpected.</summary>
    internal static bool Is(Exception ex) => ex is IOException or UnauthorizedAccessException;
}
