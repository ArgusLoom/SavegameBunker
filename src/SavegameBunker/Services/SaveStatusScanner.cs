using System.IO;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Reports what save data actually exists for one catalog entry: whether the directory exists,
/// how many files, how big, and when it last changed.
/// </summary>
/// <remarks>
/// Never throws across its boundary — everything unexpected becomes
/// <see cref="SaveStatusKind.PathError"/>. The walk, its reparse-point rules and its tolerance of
/// locked subtrees are <see cref="DirectoryWalk"/>'s, shared with <see cref="BackupService"/> so
/// the row counts what a backup archives. The scan reads directory metadata only and creates,
/// moves or removes nothing. An unreadable subtree is not a failure, but it is carried on
/// <see cref="SaveStatus.SkippedPaths"/> so the row never looks more complete than it is.
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
            // Collected the same way BackupService collects its own skip list: a skip is
            // silently absent from the walk's totals, so without this the row's count could look
            // complete while a path it could not read is missing from it.
            var skipped = new List<string>();

            DirectoryWalk.ForEachFile(dir, info =>
            {
                fileCount++;
                totalBytes += info.Length;
                if (newest is null || info.LastWriteTime > newest)
                    newest = info.LastWriteTime;
            }, onSkipped: skipped.Add);

            if (fileCount == 0)
                return skipped.Count == 0 ? SaveStatus.NoSaves() : SaveStatus.NoSaves() with { SkippedPaths = skipped };

            var found = SaveStatus.Found(fileCount, totalBytes, newest, IsOversized(fileCount, totalBytes));
            return skipped.Count == 0 ? found : found with { SkippedPaths = skipped };
        }
        catch (Exception ex)
        {
            return SaveStatus.PathError(ex.Message);
        }
    }
}
