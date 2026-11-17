using System.IO;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Reports what save data actually exists for one catalog entry: whether the directory exists,
/// how many files, how big, and when it last changed.
/// </summary>
/// <remarks>
/// The walk never throws across its boundary — everything unexpected becomes
/// <see cref="SaveStatusKind.PathError"/>, matching <see cref="BackupService"/>'s
/// never-throw-across-the-boundary shape. The tree is walked one directory level at a time via
/// <see cref="Directory.EnumerateFiles(string)"/>/<see cref="Directory.EnumerateDirectories(string)"/>
/// rather than the recursive search option: that lazy recursive enumerator throws from
/// <c>MoveNext</c> partway through a locked subtree and would abort the whole row instead of
/// skipping just that subtree. A directory reparse point (junction or symbolic link) is never
/// followed into, so a self-referencing link cannot make the walk run forever; a file reparse
/// point is skipped only when <see cref="FileSystemInfo.LinkTarget"/> proves it is an actual
/// symbolic link rather than, say, a OneDrive Files On-Demand placeholder — matching what
/// <see cref="BackupService"/> actually archives. The scan reads
/// directory metadata only — entry names, lengths and write times; it opens no file for its data
/// and creates, moves or removes nothing under the save directory.
/// </remarks>
public static class SaveStatusScanner
{
    /// <summary>Above this many total bytes a directory is flagged as unusually large.</summary>
    internal const long OversizedBytes = 5L * 1024 * 1024 * 1024;

    /// <summary>Above this many files a directory is flagged as unusually large.</summary>
    internal const long OversizedFiles = 20_000;

    /// <summary>Strictly-greater-than on both thresholds — the boundary values themselves are not flagged.</summary>
    internal static bool IsOversized(long files, long bytes) =>
        files > OversizedFiles || bytes > OversizedBytes;

    public static SaveStatus Scan(Game game)
    {
        try
        {
            if (!PathResolver.TryResolve(game.SaveDirTemplate, null, out var dir, out var error))
                return SaveStatus.PathError(error ?? "unresolved save folder");

            if (!Directory.Exists(dir))
                return SaveStatus.NoSaves();

            long fileCount = 0;
            long totalBytes = 0;
            DateTime? newest = null;

            void Walk(string current)
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(current);
                }
                catch (UnauthorizedAccessException) { return; }
                catch (DirectoryNotFoundException) { return; }
                catch (PathTooLongException) { return; }
                catch (IOException) { return; }

                foreach (var f in files)
                {
                    FileInfo info;
                    try
                    {
                        info = new FileInfo(f);
                        if (ReparseFile.IsSymlink(info))
                            continue;
                    }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (PathTooLongException) { continue; }
                    catch (IOException) { continue; }

                    fileCount++;
                    totalBytes += info.Length;
                    if (newest is null || info.LastWriteTime > newest)
                        newest = info.LastWriteTime;
                }

                IEnumerable<string> subdirs;
                try
                {
                    subdirs = Directory.EnumerateDirectories(current);
                }
                catch (UnauthorizedAccessException) { return; }
                catch (DirectoryNotFoundException) { return; }
                catch (PathTooLongException) { return; }
                catch (IOException) { return; }

                foreach (var sub in subdirs)
                {
                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(sub);
                    }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (IOException) { continue; }

                    // A junction or symbolic link back up the tree would otherwise make this
                    // recursion run forever.
                    if (attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue;

                    Walk(sub);
                }
            }

            Walk(dir);

            if (fileCount == 0)
                return SaveStatus.NoSaves();

            return SaveStatus.Found(fileCount, totalBytes, newest, IsOversized(fileCount, totalBytes));
        }
        catch (Exception ex)
        {
            return SaveStatus.PathError(ex.Message);
        }
    }
}
