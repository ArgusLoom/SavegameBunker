using System.IO;

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
    /// <see cref="BackupService"/>'s <c>EnumerateRelativeFiles</c> for which layouts prune a
    /// subtree and which deliberately do not.
    /// </param>
    /// <param name="onSkipped">
    /// Optional: called with the path of every subtree or file the walk could not read — a
    /// directory whose listing threw, a file whose attributes threw. Reparse points and excluded
    /// directories are deliberate rules, not failures, so they go to <paramref name="onExcluded"/>
    /// or nowhere, never here. Null skips silently, for callers that have no channel to report
    /// through.
    /// </param>
    /// <param name="onExcluded">
    /// Optional: called with the walk-root-relative path of each subfolder the walk steps over
    /// because of <paramref name="excludedDirs"/> — a deliberate rule, reported apart from
    /// <paramref name="onSkipped"/> so it is never counted as unreadable. Only the outermost
    /// pruned folder is reported, because the walk never descends into it. The flag is true when
    /// the folder IS one of <paramref name="excludedDirs"/> (a backup folder), and false when it is
    /// pruned only because the walk root itself is a backup folder — such a folder may hold real
    /// save data, so callers must not call it a backup folder. Reparse points are still stepped
    /// over silently. Null excludes silently.
    /// </param>
    public static void ForEachFile(
        string rootDir, Action<FileInfo> onFile, IReadOnlyCollection<string>? excludedDirs = null,
        Action<string>? onSkipped = null, Action<string, bool>? onExcluded = null)
    {
        void Skip(string path) => onSkipped?.Invoke(Path.GetRelativePath(rootDir, path));
        void Exclude(string path, bool isBackupFolder) =>
            onExcluded?.Invoke(Path.GetRelativePath(rootDir, path), isBackupFolder);

        // Excluded folders that are the walk root itself or lie inside it. Only these can ever
        // justify pruning a subfolder for lying *inside* an exclusion: a save folder that merely
        // sits inside the backup root (or inside its own archive folder) holds no archive of this
        // app below it, so nothing there is pruned — its subfolders are archived like any others.
        var excludedWithinRoot = excludedDirs is null
            ? []
            : excludedDirs.Where(excluded => PathCompare.IsSameOrInside(excluded, rootDir)).ToList();

        void Walk(string current)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (Exception ex) when (FileSystemFault.Is(ex))
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
                catch (Exception ex) when (FileSystemFault.Is(ex))
                {
                    Skip(f);
                    continue;
                }

                // The callbacks read Length/LastWriteTime, which throw only now for a file deleted
                // since it was listed (its attributes read -1 instead of throwing above).
                try
                {
                    onFile(info);
                }
                catch (Exception ex) when (FileSystemFault.Is(ex))
                {
                    Skip(f);
                }
            }

            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(current);
            }
            catch (Exception ex) when (FileSystemFault.Is(ex))
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
                catch (Exception ex) when (FileSystemFault.Is(ex))
                {
                    Skip(sub);
                    continue;
                }

                // A junction or symbolic link back up the tree would otherwise make this
                // recursion run forever.
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    continue;

                // The three layouts this prune decides between:
                // - backup root inside the save folder: prune exactly the subfolder that IS it. A
                //   subfolder that merely contains it is walked, so save files sitting beside the
                //   backup folder are archived; the recursion prunes at the backup folder itself;
                // - backup root or archive folder EQUAL to the save folder: every subfolder is
                //   pruned, because other games' archive folders cannot be told apart from save
                //   data there (the excludedWithinRoot arm) — reported as not-a-backup-folder,
                //   since it may well be save data;
                // - save folder inside the backup root or inside its own archive folder: prune
                //   nothing — no archive of this app lives below it, so its subfolders are archived.
                if (excludedDirs is not null)
                {
                    if (excludedDirs.Any(excluded => PathCompare.IsSameOrInside(excluded, sub)
                                                     && PathCompare.IsSameOrInside(sub, excluded)))
                    {
                        Exclude(sub, true);
                        continue;
                    }

                    if (excludedWithinRoot.Any(excluded => PathCompare.IsSameOrInside(sub, excluded)))
                    {
                        Exclude(sub, false);
                        continue;
                    }
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
    /// warning, the scan row's, and the left-out note BackupService appends for folders the walk
    /// leaves out on purpose — so the surfaces cannot drift apart. Names the first five,
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
