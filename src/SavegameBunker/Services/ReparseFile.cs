using System.IO;

namespace SavegameBunker.Services;

/// <summary>
/// Decides whether a reparse-flagged file should be treated as a real link (skip it) or as
/// ordinary content (keep it) — the one predicate <see cref="BackupService"/>,
/// <see cref="RestoreService"/> and <see cref="SaveStatusScanner"/> all need for the identical
/// reason: a OneDrive Files On-Demand placeholder also sets <see cref="FileAttributes.ReparsePoint"/>
/// but is real save content, not a link to somewhere else. <see cref="FileSystemInfo.LinkTarget"/>
/// is the one property the OS resolves only for an actual symbolic link
/// (<c>IO_REPARSE_TAG_SYMLINK</c>), so it is what tells the two apart — a cloud placeholder never
/// sets that tag, hydrated locally or not.
/// </summary>
internal static class ReparseFile
{
    /// <summary>True when <paramref name="info"/> is an actual symbolic link that must not be
    /// followed, counted or archived.</summary>
    public static bool IsSymlink(FileInfo info) =>
        info.Attributes.HasFlag(FileAttributes.ReparsePoint) && info.LinkTarget is not null;

    /// <summary>
    /// Whether <paramref name="dir"/> — already confirmed to exist — holds a file anywhere in its
    /// tree. Recursive; stops at the first file found rather than materialising the whole tree.
    /// Shared by <see cref="BackupService"/> (its own empty-check) and <see cref="RestoreService"/>
    /// (deciding whether a pre-restore safety backup is owed) — both need the identical answer.
    /// Symbolic links are skipped, so a folder holding only a symlinked file reads as empty in both,
    /// and directory reparse points are never entered, so a self-referencing junction cannot make
    /// the recursion run forever. A subdirectory
    /// that throws while being listed is treated as contributing no files; only a throw from the
    /// call naming <paramref name="dir"/> itself propagates to the caller.
    /// </summary>
    public static bool HasAnyFile(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir))
        {
            try
            {
                if (!IsSymlink(new FileInfo(f)))
                    return true;
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }
        }

        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(sub);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                continue;

            try
            {
                if (HasAnyFile(sub))
                    return true;
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            catch (PathTooLongException) { continue; }
            catch (IOException) { continue; }
        }

        return false;
    }
}
