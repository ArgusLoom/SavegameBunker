using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Restores a game's save data from a backup archive written by <see cref="BackupService"/>.
/// </summary>
/// <remarks>
/// The restore ordering — safety backup → extract to a temp sibling directory → remove the old
/// directory → rename the new one in — is the entire safety argument for the app's only destructive
/// operation and is a one-way decision: changing it later invalidates
/// the restore guarantees all at once, and there is no non-destructive fallback to migrate
/// to. The temp extraction directory is a same-volume sibling of the save directory so the final
/// swap is a rename, not a cross-volume copy. The pre-restore safety backup reuses
/// <see cref="BackupService.Backup(Game, string, string, out string?, IProgress{long}?)"/> rather than a second
/// archive writer. A failed safety backup aborts before anything else is written, and the
/// confirmation delegate is never invoked.
/// </remarks>
public sealed class RestoreService
{
    /// <summary>Above this many zip entries a restore is refused before extraction (zip-bomb cap).</summary>
    internal const int MaxArchiveEntries = 100_000;

    /// <summary>Above this many uncompressed bytes a restore is refused before extraction.</summary>
    internal const long MaxArchiveUncompressedBytes = 32L * 1024 * 1024 * 1024;

    private readonly BackupService _backup;

    public RestoreService(BackupService backup)
    {
        _backup = backup;
    }

    /// <param name="game">The catalog entry whose save directory is being restored into.</param>
    /// <param name="backupRoot">Absolute path of the configured backup folder.</param>
    /// <param name="archivePath">
    /// The archive to restore, as picked in the restore picker. Must resolve under
    /// <c>Path.Combine(backupRoot, game.Id)</c> using the same <paramref name="backupRoot"/> value
    /// <see cref="BackupService.ListBackups"/> was called with to produce it — a caller that
    /// re-resolves a relative, catalog-level backupRoot differently between listing and restoring
    /// gets every restore refused here with a generic "not a backup" message.
    /// </param>
    /// <param name="confirm">
    /// Invoked exactly once, after the safety backup has been written (or determined unnecessary)
    /// and before anything about the live save directory changes. A false return cancels the
    /// restore — nothing is extracted, nothing is swapped, and the safety backup already written,
    /// if any, is deleted again: nothing was replaced, so there is nothing for it to protect, and
    /// leaving it behind would present a declined restore as a backup the user never asked for.
    /// </param>
    /// <param name="progress">
    /// Optional. Receives the step number (1, 2, 3) immediately before each of the three ordered
    /// stages below begins — the safety backup, the extraction and the swap. Reported from here
    /// rather than from a caller because the boundary between extraction and swap is not
    /// observable from outside this method, and a caller's guess would be invented progress.
    /// </param>
    public OperationResult Restore(
        Game game,
        string backupRoot,
        string archivePath,
        Func<RestorePrompt, bool> confirm,
        IProgress<int>? progress = null)
    {
        try
        {
            if (!PathResolver.TryResolve(game.SaveDirTemplate, null, out var saveDir, out var pathError))
                return OperationResult.Fail($"Save folder for {game.DisplayName}: {pathError}");

            // Restore replaces the save folder wholesale: the old one is renamed aside and then
            // deleted. When the game's own archive folder (<backupRoot>/<id>) is that folder or
            // lies inside it, every archive of the game — including the safety backup this method
            // is about to write — would be deleted with it. Backing up in such a layout stays
            // supported; replacing the folder that holds the archives is not. The check covers
            // every backupRoot inside or equal to the save folder too, because it puts the game's
            // archive folder inside the save folder as well.
            var gameArchiveDir = Path.Combine(backupRoot, game.Id);
            if (PathCompare.IsSameOrInside(gameArchiveDir, saveDir))
                return OperationResult.Fail(Strings.Format("str_restore_backupFolderInsideSaveFolder", game.DisplayName));

            // The same loss through another game's archive folder: a save folder that is
            // <backupRoot>/<otherId> holds that game's archives, which restore would delete with
            // it. A save folder that merely sits inside the backup root (backup folder Documents,
            // save folder Documents/MyGame) stays restorable, so this refuses only when the folder
            // directly under the backup root actually holds archives this app wrote.
            var saveParent = Path.GetDirectoryName(PathCompare.Normalize(saveDir));
            if (saveParent is not null
                && PathCompare.IsSameOrInside(saveParent, backupRoot)
                && PathCompare.IsSameOrInside(backupRoot, saveParent)
                && Directory.Exists(saveDir)
                && Directory.EnumerateFiles(saveDir).Any(f => BackupService.IsOwnArchiveFileName(Path.GetFileName(f))))
                return OperationResult.Fail(Strings.Format("str_restore_backupFolderInsideSaveFolder", game.DisplayName));

            // The archive must live in this game's own backup folder (<backupRoot>/<game.Id>) —
            // the same containment logic the backup walk uses for exclusions, pointed at the
            // single archive. ListBackups already enforces that layout; this guard keeps any
            // caller from pointing restore at an arbitrary zip outside the app's own archives.
            // IsSameOrInside compares normalized path prefixes, so it works on a file path too.
            if (!PathCompare.IsSameOrInside(archivePath, gameArchiveDir))
                return OperationResult.Fail(
                    $"Archive is not a backup of {game.DisplayName} in the configured backup folder: {archivePath}");

            if (ExceedsArchiveLimits(archivePath))
                return OperationResult.Fail(Strings.Format("str_restore_archiveTooLarge", game.DisplayName));

            // Clears this one game's own leftover restore siblings before anything else runs, so
            // a run that has already failed its path check above never sweeps anything.
            CleanStaleRestoreTemp(saveDir);

            progress?.Report(1);

            // A safety backup is owed only when there is something in the save directory to
            // protect — restoring into a missing or empty save folder is the classic restore use
            // case, and the abort rule is for a safety backup that failed, not one with nothing to
            // capture. The step is reported either way: the stage is walked, it just has no work.
            string? safetyPath = null;
            if (Directory.Exists(saveDir) && ReparseFile.HasAnyFile(saveDir))
            {
                var safetyResult = _backup.Backup(game, backupRoot, "-pre-restore", out safetyPath);
                if (!safetyResult.Success)
                    return safetyResult;
            }

            if (!confirm(new RestorePrompt(saveDir, safetyPath)))
            {
                // The safety backup existed to cover the replacement the user just declined. With
                // nothing replaced it protects nothing, and keeping it would leave one more
                // archive behind for every restore that was only looked at.
                if (safetyPath is not null)
                {
                    try { File.Delete(safetyPath); } catch { /* best effort — an extra archive is harmless */ }
                }

                return OperationResult.Cancel($"Restore cancelled for {game.DisplayName}.");
            }

            progress?.Report(2);

            // A sibling of saveDir, on the same volume, so the eventual swap is a rename.
            var tempDir = $"{saveDir}.savegamebunker-restore-tmp-{Guid.NewGuid():N}";
            try
            {
                ZipFile.ExtractToDirectory(archivePath, tempDir, overwriteFiles: false);
            }
            catch (Exception ex)
            {
                TryDeleteDirectory(tempDir);
                return OperationResult.Fail($"Restore failed for {game.DisplayName}: {ex.Message}");
            }

            // The same internal method backup uses for (justWrittenZip, saveDir), pointed in the
            // opposite direction — this is the round-trip proof. Nothing about the live save
            // path has been touched yet.
            //
            // No expectedHashes: restore has none recorded, so the no-hashes mode hashes every
            // entry's decompressed stream against the extracted file on disk — .NET 10 does not
            // validate an entry's CRC-32 on read and no stored hash sidecar exists, so hashing
            // both sides is what detects a bit flip inside a structurally valid archive.
            // Extracted-but-corrupt fails before the swap, never after.
            var verificationFailure = BackupService.DescribeVerificationFailure(archivePath, tempDir);
            if (verificationFailure is not null)
            {
                TryDeleteDirectory(tempDir);
                return OperationResult.Fail($"Restore verification failed for {archivePath}: {verificationFailure}.");
            }

            progress?.Report(3);

            // "Remove the old directory" is a single same-volume rename, never a recursive delete
            // (dotnet/runtime#27958): a locked file partway through a recursive
            // delete leaves a mixture of old and new content, exactly what restore must never leave behind. Skipped
            // entirely when the save directory does not exist — there is nothing to rename aside.
            string? oldAsideDir = null;
            if (Directory.Exists(saveDir))
            {
                oldAsideDir = $"{saveDir}.savegamebunker-restore-old-{Guid.NewGuid():N}";
                try
                {
                    Directory.Move(saveDir, oldAsideDir);
                }
                catch (Exception ex)
                {
                    // tempDir, already verified, is left in place for the next attempt.
                    return OperationResult.Fail($"Restore failed for {game.DisplayName}: the save folder is in use ({ex.Message}).");
                }
            }

            // Same-volume rename — a single MFT directory-entry operation, atomic. Guarded
            // on its own: if this specific rename fails after the previous one already moved the
            // live save directory aside, saveDir now exists nowhere, and the best recovery is
            // putting the old directory straight back rather than leaving the game with no save
            // folder at all until the user notices and retries.
            try
            {
                Directory.Move(tempDir, saveDir);
            }
            catch (Exception ex)
            {
                var oldRestored = false;
                if (oldAsideDir is not null)
                {
                    try
                    {
                        Directory.Move(oldAsideDir, saveDir);
                        oldAsideDir = null;
                        oldRestored = true;
                    }
                    catch { /* best effort — see CleanStaleRestoreTemp for why the aside is never auto-deleted */ }
                }

                // The live save directory is intact again, so the extracted copy is redundant —
                // the archive it came from still exists. Only the failure that could NOT restore
                // the old directory keeps tempDir, because then it holds the user's only new-copy
                // content worth keeping.
                if (oldRestored)
                    TryDeleteDirectory(tempDir);

                // Not "the save folder is in use": the cause may be anything (a disk error, a
                // path-length overflow), and the actual exception message is what the user can act on.
                return OperationResult.Fail(
                    $"Restore failed for {game.DisplayName}: the restored folder could not be put in place ({ex.Message}).");
            }

            // Best-effort — a failure here changes nothing about correctness. The safety backup
            // already secured the pre-restore data, so deletion timing of the old aside directory
            // is irrelevant to the guarantee.
            if (oldAsideDir is not null)
                TryDeleteDirectory(oldAsideDir);

            return OperationResult.Ok(Strings.Format("str_restore_done", game.DisplayName));
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Restore failed for {game.DisplayName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether the archive's declared entry count or uncompressed size exceeds the zip-bomb caps.
    /// Reads the central directory only — no extraction. Restore refuses such an archive, and
    /// <see cref="BackupService"/> warns when it has just written one. An unreadable archive
    /// counts as within limits: restore then fails at extraction with the real IO error. The
    /// limits are parameters so tests can drive the caps without a 32 GB file.
    /// </summary>
    internal static bool ExceedsArchiveLimits(
        string archivePath,
        int maxEntries = MaxArchiveEntries,
        long maxUncompressedBytes = MaxArchiveUncompressedBytes)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var entries = 0;
            var uncompressed = 0L;
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/'))
                    continue;

                entries++;
                uncompressed += entry.Length;
                if (entries > maxEntries || uncompressed > maxUncompressedBytes)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Best-effort cleanup — a failed delete must never replace the original error.</summary>
    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best effort */ }
    }

    /// <summary>
    /// Deletes every leftover <c>&lt;leaf-name&gt;.savegamebunker-restore-tmp-*</c> sibling of
    /// <paramref name="saveDir"/> before this attempt writes anything — per game, never a startup
    /// sweep, for the same reason as <see cref="BackupService"/>'s <c>CleanStaleTempFiles</c>. The
    /// sweep deletes by name, which is safe because only <see cref="Restore"/> creates that pattern
    /// and it embeds the save directory's leaf name, so it cannot reach another game's folder.
    /// Reports nothing: housekeeping the user never knew about is not an event they can act on.
    /// </summary>
    /// <remarks>
    /// Never matches <c>-old-</c> siblings: one of those is the live save data that an earlier
    /// attempt's final swap failed to put back. Sweeping it before a retry would delete the user's
    /// only remaining copy.
    /// </remarks>
    private static void CleanStaleRestoreTemp(string saveDir)
    {
        var parent = Path.GetDirectoryName(saveDir);
        if (parent is null || !Directory.Exists(parent))
            return;

        var leafName = Path.GetFileName(saveDir);

        List<string> stale;
        try
        {
            stale = [.. Directory.EnumerateDirectories(parent, $"{leafName}.savegamebunker-restore-tmp-*")];
        }
        catch
        {
            return;
        }

        foreach (var dir in stale)
            TryDeleteDirectory(dir);
    }
}
