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

    /// <param name="backupRoot">
    /// The configured backup folder, when known: the scan then applies the backup walk's own
    /// exclusions (<see cref="BackupService.ArchiveWalkRules"/>), so archives this app wrote are
    /// never counted as save data — otherwise every fresh archive would read as "saves changed
    /// since backup".
    /// </param>
    public static SaveStatus Scan(Game game, string? backupRoot = null)
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

            var leftOut = new List<string>();

            // The backup walk's own rules (BackupService.ArchiveWalkRules), so a backup folder
            // inside the save folder and this app's archives at its root are never counted as save
            // data — their fresh archives would otherwise read as "saves changed since backup".
            IReadOnlyCollection<string>? excludedDirs = null;
            var skipOwnArchives = false;
            if (!string.IsNullOrWhiteSpace(backupRoot))
                (excludedDirs, skipOwnArchives) = BackupService.ArchiveWalkRules(dir, backupRoot, Path.Combine(backupRoot, game.Id));

            DirectoryWalk.ForEachFile(dir, info =>
            {
                if (skipOwnArchives
                    && PathCompare.IsSameOrInside(dir, info.DirectoryName ?? "")
                    && PathCompare.IsSameOrInside(info.DirectoryName ?? "", dir)
                    && BackupService.IsOwnArchiveFileName(info.Name))
                    return;

                // Read first: a file deleted since it was listed throws here and the walk leaves it
                // out silently — it must not have been counted already.
                var length = info.Length;
                var lastWrite = info.LastWriteTime;
                fileCount++;
                totalBytes += length;
                if (newest is null || lastWrite > newest)
                    newest = lastWrite;
            }, excludedDirs, onSkipped: skipped.Add,
                // A subfolder pruned only because the save folder IS the backup folder may hold
                // real saves that no backup captures; it must surface, never vanish into "No saves".
                onExcluded: (path, isBackupFolder) =>
                {
                    if (!isBackupFolder)
                        leftOut.Add(path);
                });

            var status = fileCount == 0
                ? SaveStatus.NoSaves()
                : SaveStatus.Found(fileCount, totalBytes, newest, IsOversized(fileCount, totalBytes));
            if (skipped.Count > 0)
                status = status with { SkippedPaths = skipped };
            if (leftOut.Count > 0)
                status = status with { LeftOutPaths = leftOut };
            return status;
        }
        catch (Exception ex)
        {
            return SaveStatus.PathError(ex.Message);
        }
    }
}
