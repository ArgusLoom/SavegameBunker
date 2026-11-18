using System.IO;
using System.Linq;

namespace SavegameBunker.Services;

/// <summary>
/// The one-level-at-a-time directory walk <see cref="SaveStatusScanner"/> and
/// <see cref="BackupService"/> both perform, kept in one place so the exception handling,
/// reparse-point rules and symlink skip cannot drift apart between them.
/// </summary>
/// <remarks>
/// Never throws across its boundary — every exception a locked or vanished subtree can raise is
/// swallowed and that subtree is simply skipped — optionally reported via the callback — matching
/// both callers' never-throw-across-the-boundary shape. The tree is walked via
/// <see cref="Directory.EnumerateFiles(string)"/>/
/// <see cref="Directory.EnumerateDirectories(string)"/> rather than the recursive search option:
/// that lazy recursive enumerator throws from <c>MoveNext</c> partway through a locked subtree and
/// would abort the whole walk instead of skipping just that subtree. A directory reparse point
/// (junction or symbolic link) is never followed into, so a self-referencing link cannot make the
/// walk run forever; a file reparse point is skipped only when
/// <see cref="FileSystemInfo.LinkTarget"/> proves it is an actual symbolic link, rather than, say,
/// a OneDrive Files On-Demand placeholder.
/// </remarks>
internal static class DirectoryWalk
{
    /// <summary>
    /// Walks <paramref name="rootDir"/>, calling <paramref name="onFile"/> for every real file
    /// found (never a symlink).
    /// </summary>
    /// <param name="excludedDirs">
    /// Directories to step over — see the <c>excludedDirs</c> parameter of
    /// <see cref="BackupService"/>'s <c>EnumerateRelativeFiles</c> for why containment in either
    /// direction, not an exact match alone, is what prunes a subtree.
    /// </param>
    /// <param name="onSkipped">
    /// Optional: called with the path of every subtree or file the walk could not read — a
    /// directory whose listing threw, a file whose attributes threw. Deliberate skips (reparse
    /// points, excluded directories) are never reported: those are rules, not failures. Null
    /// skips silently, for callers that have no channel to report through.
    /// </param>
    public static void ForEachFile(
        string rootDir, Action<FileInfo> onFile, IReadOnlyCollection<string>? excludedDirs = null,
        Action<string>? onSkipped = null)
    {
        void Skip(string path) => onSkipped?.Invoke(Path.GetRelativePath(rootDir, path));

        void Walk(string current)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or IOException)
            {
                Skip(current);
                return;
            }

            foreach (var f in files)
            {
                FileInfo info;
                try
                {
                    info = new FileInfo(f);
                    if (ReparseFile.IsSymlink(info))
                        continue;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
                {
                    Skip(f);
                    continue;
                }

                onFile(info);
            }

            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or PathTooLongException or IOException)
            {
                Skip(current);
                return;
            }

            foreach (var sub in subdirs)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(sub);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    Skip(sub);
                    continue;
                }

                // A junction or symbolic link back up the tree would otherwise make this
                // recursion run forever.
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;

                if (excludedDirs is not null
                    && excludedDirs.Any(excluded =>
                        BackupService.IsSameOrInside(sub, excluded) || BackupService.IsSameOrInside(excluded, sub)))
                {
                    continue;
                }

                Walk(sub);
            }
        }

        Walk(rootDir);
    }

    /// <summary>
    /// Counts the real files under <paramref name="rootDir"/> and sums their length, via
    /// <see cref="ForEachFile"/> — the shared walk used by every folder-size preview.
    /// </summary>
    public static (int Count, long Bytes) CountFiles(string rootDir)
    {
        var count = 0;
        var bytes = 0L;
        ForEachFile(rootDir, info =>
        {
            count++;
            bytes += info.Length;
        });
        return (count, bytes);
    }

    /// <summary>
    /// The one display shape for a skipped-path list, shared by <see cref="BackupService"/>'s own
    /// warning and the scan row's — so the two surfaces cannot drift apart. Names the first five,
    /// comma-joined, and counts the rest (<c>", and {n} more"</c>) when there are more than five.
    /// </summary>
    public static string FormatSkippedList(IReadOnlyCollection<string> skippedPaths)
    {
        var shown = string.Join(", ", skippedPaths.Take(5));
        if (skippedPaths.Count > 5)
            shown += $", and {skippedPaths.Count - 5} more";
        return shown;
    }
}
