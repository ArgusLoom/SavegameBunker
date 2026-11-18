using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Zips a game's save directory into the backup folder the user configured.
/// </summary>
/// <remarks>
/// The destination is whatever folder was chosen — this class never inspects what kind of volume
/// it sits on. An earlier version accepted removable drives only and refused everything else; that
/// rule cost users the ability to back up at all when no USB stick was connected, and it was wrong
/// on its own terms because external SSDs report as <see cref="DriveType.Fixed"/>. A folder that
/// exists and can be written to is the whole requirement.
/// <para>
/// "Can be written to" is proven by an actual write-then-delete probe against the destination
/// (<see cref="EnsureWritable"/>), never by inspecting <see cref="DriveInfo"/> or an ACL — only a
/// real write distinguishes a read-only share, a drive letter reassigned since the last run and a
/// folder deleted out from under the configured path. The reasoning an earlier revision withdrew
/// was the same-volume *warning*, not this volume-agnostic writability rule.
/// </para>
/// <para>
/// This class now both writes and lists archives — <see cref="ListBackups"/> is the read-only
/// query restore consumes for its picker, its save-vs-backup verdict and its pre-restore
/// safety backup.
/// </para>
/// </remarks>
public sealed class BackupService
{
    /// <summary>
    /// The number of suffixed names tried after the bare one before the final rename gives up —
    /// the bare <c>yyyy-MM-dd_HHmmss.zip</c> plus <c>-1</c> through <c>-100</c>. At the limit
    /// (every one of those 101 names already taken for the stamped second) the backup fails with
    /// its own "rename retry limit reached" message rather than surfacing the underlying
    /// <see cref="IOException"/> as a generic disk error, and the already-verified staging file
    /// is deleted as on every other failure path. The ladder's length is unchanged by naming it.
    /// </summary>
    private const int MaxRenameRetries = 100;

    /// <param name="backupRoot">
    /// Absolute path of the configured backup folder, already resolved by the caller. Created if
    /// it does not exist yet.
    /// </param>
    /// <param name="progress">
    /// Optional cumulative-bytes-archived reporter, carried straight through to
    /// <see cref="ArchiveSaveDir"/> — the only place the per-file boundary is actually observable.
    /// Never required: every existing caller keeps compiling unchanged.
    /// </param>
    public OperationResult Backup(Game game, string backupRoot, IProgress<long>? progress = null) =>
        Backup(game, backupRoot, "", out _, progress);

    /// <param name="backupRoot">
    /// Absolute path of the configured backup folder, already resolved by the caller. Created if
    /// it does not exist yet.
    /// </param>
    /// <param name="suffix">
    /// Appended to the timestamp in the archive's file name before <c>.zip</c> — empty for an
    /// ordinary backup, <c>"-pre-restore"</c> for the safety backup restore takes before
    /// it writes anything. <see cref="ParseTakenAt"/> already falls back to the file's own
    /// last-write time for a name that does not parse as a bare timestamp, so a suffixed archive
    /// stays visible in the read-only listing query instead of vanishing from it.
    /// </param>
    /// <param name="archivePath">The written archive's final path on success; null on any failure.</param>
    /// <param name="progress">
    /// Optional cumulative-bytes-archived reporter — reported
    /// from inside <see cref="ArchiveSaveDir"/>, never before a save-directory check has already
    /// passed and never across the service boundary as an exception; reporting is display only.
    /// </param>
    public OperationResult Backup(Game game, string backupRoot, string suffix, out string? archivePath, IProgress<long>? progress = null)
    {
        archivePath = null;

        try
        {
            if (!PathResolver.TryResolve(game.SaveDirTemplate, null, out var saveDir, out var pathError))
                return OperationResult.Fail($"Save folder for {game.DisplayName}: {pathError}");

            if (!Directory.Exists(saveDir))
                return OperationResult.Fail($"Save folder not found: {saveDir}");

            switch (InspectSaveDir(saveDir))
            {
                case SaveDirContent.Empty:
                    return OperationResult.Fail($"Save folder is empty: {saveDir}");
                case SaveDirContent.Unreadable:
                    return OperationResult.Fail($"Save folder could not be read: {saveDir}");
            }

            // <backupRoot>/<id>/<yyyy-MM-dd_HHmmss>.zip — one folder per game, so restore has a
            // single place to look and archives of different games never interleave.
            var targetDir = Path.Combine(backupRoot, game.Id);

            // Runs before anything is created for a backup that has already failed a
            // save-directory check, and owns the one call that creates targetDir.
            var writabilityFailure = EnsureWritable(backupRoot, targetDir);
            if (writabilityFailure is not null)
                return writabilityFailure;

            CleanStaleTempFiles(targetDir);

            // The staging file's name carries no timestamp of its own — the archive's timestamp is
            // taken after the zip is written and verified, not before, so a long-running backup
            // never dates itself earlier than the save files it just captured (which would make the
            // save-vs-backup heuristic read "changed since backup" on a backup that just succeeded).
            var tempPath = Path.Combine(targetDir, $".{Guid.NewGuid():N}.zip.tmp");

            // Collected from the archive walk: every path the walk could not read. A skipped path
            // is silently absent from the archive, and the verification walk skips it the same way
            // — so without this list an incomplete archive would verify clean. Reported on the
            // result below; a hard failure on any unreadable path would turn a locked junk file
            // into a refused backup, so incompleteness is surfaced as a warning instead.
            var skippedPaths = new List<string>();

            // A backup folder the user placed inside this game's own save folder is honoured like
            // any other destination — but the walk must step over it, or every backup would pack
            // all earlier archives (and trip over its own half-written staging file). targetDir is
            // listed too for the case where the backup folder IS the save folder.
            var excludedDirs = new[] { NormalizeDir(backupRoot), NormalizeDir(targetDir) };

            Dictionary<string, string> entryHashes;
            try
            {
                // Own walk, not ZipFile.CreateFromDirectory: that API follows directory junctions
                // and would archive whatever they point at (Documents, SSH keys, …). The empty-check
                // and the scanner already skip reparse points; the zip must skip them too.
                entryHashes = ArchiveSaveDir(saveDir, tempPath, progress, excludedDirs, skippedPaths);
            }
            catch (Exception ex)
            {
                // A failed cleanup must never replace the original error.
                TryDelete(tempPath);
                return OperationResult.Fail($"Backup failed for {game.DisplayName}: {ex.Message}");
            }

            // Success is reported only after the archive is reopened and its entries matched
            // against a fresh walk of the save directory.
            var verificationFailure = DescribeVerificationFailure(
                tempPath, saveDir, excludedDirs, skippedPaths, entryHashes);
            if (verificationFailure is not null)
            {
                TryDelete(tempPath);
                return OperationResult.Fail($"Backup verification failed for {game.DisplayName}: {verificationFailure}.");
            }

            // Invariant, matching ParseTakenAt: the user's culture may default to a non-Gregorian
            // calendar (th-TH, fa-IR, ar-SA) and would name the archive in that calendar's year.
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture);
            var finalPath = Path.Combine(targetDir, $"{timestamp}{suffix}.zip");

            try
            {
                // The two-argument Move overload, not a swap-style replace: finalPath is never
                // overwritten — a same-second collision (two backups of the same game in one
                // second, or the same wall-clock second recurring across a DST fall-back) used to
                // make File.Move throw and then delete the already-verified staging file here.
                // Instead the name is suffixed -1, -2, … and retried until MaxRenameRetries is
                // exhausted, where the limit gets its own message below; the suffixed name breaks
                // ParseTakenAt's bare-timestamp match, whose documented fallback (the file's own
                // last-write time — the same one a renamed or -pre-restore archive relies on)
                // keeps a suffixed archive visible in the listing.
                for (var attempt = 0; ; attempt++)
                {
                    var candidate = attempt == 0
                        ? finalPath
                        : Path.Combine(targetDir, $"{timestamp}{suffix}-{attempt}.zip");
                    try
                    {
                        File.Move(tempPath, candidate);
                        finalPath = candidate;
                        break;
                    }
                    catch (IOException) when (attempt < MaxRenameRetries && File.Exists(candidate))
                    {
                        // Candidate pre-exists: try the next suffix. Anything other than a
                        // name collision (disk full, vanished target dir, …) escapes to the
                        // catch below and is reported as before.
                    }
                    catch (IOException) when (attempt == MaxRenameRetries && File.Exists(candidate))
                    {
                        // The suffix ladder is exhausted — the bare name and every numbered
                        // variant are taken. Distinguishable from a disk error, so the user is
                        // told to make room or point the backup somewhere else rather than
                        // reading a File.Move exception as a failing disk.
                        TryDelete(tempPath);
                        return OperationResult.Fail(
                            $"Backup failed for {game.DisplayName}: rename retry limit reached — {timestamp}{suffix}.zip and every numbered variant up to -{MaxRenameRetries} already exist in {targetDir}.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Same cleanup discipline as the two failure paths above it — the already-verified
                // staging file is not left for CleanStaleTempFiles to find on some later backup.
                TryDelete(tempPath);
                return OperationResult.Fail($"Backup failed for {game.DisplayName}: {ex.Message}");
            }

            archivePath = finalPath;

            var createdMessage = Strings.Format("str_backup_created", game.DisplayName);
            if (skippedPaths.Count > 0)
                createdMessage += DescribeSkippedPathsWarning(skippedPaths);
            var oversizedWarning = DescribeOversizedArchiveWarning(finalPath);
            return OperationResult.Ok(oversizedWarning is null
                ? createdMessage
                : $"{createdMessage} {oversizedWarning}");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Backup failed for {game.DisplayName}: {ex.Message}");
        }
    }

    /// <summary>
    /// The warning appended to a backup's success message when its walk had to skip paths —
    /// unreadable subtrees, files whose attributes could not be stat'ed. Names up to five of the
    /// skipped paths so the user can act on them, and says plainly that they are absent from the
    /// archive rather than leaving "created" to imply completeness it does not have.
    /// </summary>
    private static string DescribeSkippedPathsWarning(IReadOnlyCollection<string> skippedPaths) =>
        $" Warning: {skippedPaths.Count} path(s) could not be read and are NOT in this backup: {DirectoryWalk.FormatSkippedList(skippedPaths)}";

    /// <summary>
    /// Warns when the archive just written exceeds <see cref="RestoreService"/>'s own zip-bomb
    /// caps (<see cref="RestoreService.MaxArchiveEntries"/>,
    /// <see cref="RestoreService.MaxArchiveUncompressedBytes"/>). Backup itself is never capped —
    /// refusing to protect data because it is large would be worse than the risk those caps guard
    /// against — but an archive this app cannot later restore is worth telling the user about at
    /// the moment it is created, not the moment a restore refuses it.
    /// </summary>
    private static string? DescribeOversizedArchiveWarning(string archivePath) =>
        RestoreService.ExceedsArchiveLimits(archivePath) ? Strings.Get("str_backup_exceedsRestoreCap") : null;

    /// <summary>
    /// Whether <paramref name="backupRoot"/> currently exists — a bare read-only existence probe
    /// that creates nothing, unlike <see cref="Backup(Game, string, string, out string?, IProgress{long}?)"/>'s own
    /// writability check, which writes a probe file and creates the per-game folder as a side
    /// effect. That side effect is correct before an actual backup but wrong here: merely drawing a
    /// status row must not create a folder for every catalogued game. Never throws across the
    /// service boundary — the one deliberate bare <c>catch</c>, same contract as the read-only
    /// listing query's own.
    /// </summary>
    public bool IsBackupRootReachable(string backupRoot)
    {
        try { return Directory.Exists(backupRoot); }
        catch { return false; }
    }

    /// <summary>
    /// Lists <paramref name="gameId"/>'s archives under <paramref name="backupRoot"/>, newest
    /// first. Read-only: creates, moves and deletes nothing, and never throws across the service
    /// boundary — an unreachable <paramref name="backupRoot"/>, an absent game folder and a folder
    /// that cannot be listed all report as an empty list, which is "no backups yet" rather than a
    /// failure this method has no channel to report. Callers that need to tell those two apart use
    /// <see cref="TryListBackups"/> instead.
    /// </summary>
    public IReadOnlyList<BackupArchive> ListBackups(string backupRoot, string gameId) =>
        ListBackupsCore(backupRoot, gameId, out _);

    /// <summary>
    /// Same listing as <see cref="ListBackups"/>, plus whether the folder was actually listable.
    /// Exists for the restore pre-flight (<c>GameActions.CheckRestorePreconditionsAsync</c>):
    /// "no backups for this game" and "the folder exists but couldn't be read" (a permission-denied
    /// share, say) used to report as the identical empty list, which sent the user to make a first
    /// backup for a game that already had ones — into a folder the app just proved it can't read.
    /// </summary>
    /// <returns>False when the game's folder exists but enumerating it failed.</returns>
    public bool TryListBackups(string backupRoot, string gameId, out IReadOnlyList<BackupArchive> backups)
    {
        backups = ListBackupsCore(backupRoot, gameId, out var listable);
        return listable;
    }

    private static IReadOnlyList<BackupArchive> ListBackupsCore(string backupRoot, string gameId, out bool listable)
    {
        listable = true;
        try
        {
            // Composed exactly as Backup composes targetDir, so the two can never drift apart.
            var targetDir = Path.Combine(backupRoot, gameId);
            if (!Directory.Exists(targetDir))
                return [];

            // Directory.GetFiles("*.zip") also matches files whose 8.3 short name ends in .zip, so
            // a *.zip.tmp staging file is not guaranteed excluded by the glob alone — the explicit
            // EndsWith is what actually enforces that exclusion.
            //
            // The projection is guarded per file (audit A5): the lazy Select ran while the list
            // was being sorted, so an archive deleted by the user (or another cleanup) between
            // GetFiles and its FileInfo.Length read threw FileNotFoundException and the bare
            // catch below misreported the whole folder as unlistable. A file that has vanished
            // simply drops out of the list.
            return Directory.GetFiles(targetDir, "*.zip")
                .Where(path => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .Select(path =>
                {
                    try
                    {
                        return new BackupArchive(path, ParseTakenAt(path), new FileInfo(path).Length);
                    }
                    catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException)
                    {
                        return null;
                    }
                })
                .OfType<BackupArchive>()
                .OrderByDescending(b => b.TakenAt)
                .ThenByDescending(b => b.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            // The one bare catch in this class that is correct: a read-only query returning a
            // list, not an OperationResult, has no channel of its own for an error to travel
            // through — that is what the out parameter is for.
            listable = false;
            return [];
        }
    }

    /// <summary>
    /// Deletes exactly the one archive at <paramref name="archivePath"/> — the restore picker's
    /// per-row delete. Never recursive, never touching anything but this single
    /// file: the caller's confirmation has covered this one ZIP and nothing else. Never throws
    /// across the service boundary — an archive that cannot be deleted (another program holds it
    /// open, a read-only share, a file removed since it was listed) comes back as a
    /// <see cref="OperationResult.Fail"/> whose message the picker shows verbatim.
    /// </summary>
    public OperationResult DeleteArchive(string archivePath)
    {
        try
        {
            // File.Delete on a missing path is a silent no-op; a row the user confirmed deleting
            // that has meanwhile vanished is worth naming rather than reporting as a success.
            if (!File.Exists(archivePath))
                return OperationResult.Fail($"Archive not found: {archivePath}");

            File.Delete(archivePath);
            return OperationResult.Ok($"Archive deleted: {archivePath}");
        }
        catch (IOException ex)
        {
            // A locked archive — most commonly the zip open in another program — is the expected
            // failure here, so it gets its own plainly-worded message.
            return OperationResult.Fail(
                $"The archive could not be deleted — it may be open in another program: {archivePath} ({ex.Message})");
        }
        catch (UnauthorizedAccessException ex)
        {
            return OperationResult.Fail(
                $"The archive could not be deleted — access is denied: {archivePath} ({ex.Message})");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Could not delete archive: {archivePath} ({ex.Message})");
        }
    }

    /// <summary>
    /// Recovers when an archive was taken from its file name, formatted with the same
    /// <c>yyyy-MM-dd_HHmmss</c> pattern <see cref="Backup(Game, string, string, out string?, IProgress{long}?)"/> writes with. Falls back to the file's
    /// own last-write time for a name that does not parse — a hand-renamed or hand-copied archive,
    /// or one of restore's <c>-pre-restore</c>-suffixed safety backups — so it stays listed instead
    /// of becoming invisible.
    /// </summary>
    private static DateTime ParseTakenAt(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return DateTime.TryParseExact(name, "yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : File.GetLastWriteTime(filePath);
    }

    /// <summary>Outcome of <see cref="InspectSaveDir"/> — distinguishes a save folder with nothing
    /// in it from one that could not be listed at all, so the two reach the user as different
    /// messages.</summary>
    private enum SaveDirContent { HasFiles, Empty, Unreadable }

    /// <summary>
    /// Reports whether <paramref name="saveDir"/> — already confirmed to exist — holds a file
    /// anywhere in its tree. <see cref="SaveDirContent.Unreadable"/> is returned only when the
    /// root itself cannot be listed; a subdirectory that cannot be listed simply contributes no
    /// files, matching <see cref="SaveStatusScanner"/>'s tolerance so a single locked subfolder
    /// never turns a real save folder into an error.
    /// </summary>
    private static SaveDirContent InspectSaveDir(string saveDir)
    {
        try
        {
            return ReparseFile.HasAnyFile(saveDir) ? SaveDirContent.HasFiles : SaveDirContent.Empty;
        }
        catch (UnauthorizedAccessException) { return SaveDirContent.Unreadable; }
        catch (DirectoryNotFoundException) { return SaveDirContent.Unreadable; }
        catch (PathTooLongException) { return SaveDirContent.Unreadable; }
        catch (IOException) { return SaveDirContent.Unreadable; }
    }

    /// <summary>
    /// Proves <paramref name="targetDir"/> is currently writable by an actual write-then-delete
    /// probe, never by inspecting <see cref="DriveInfo"/> or an ACL — only a real write
    /// distinguishes a read-only share, a drive letter reassigned since the last run, and a folder
    /// deleted since it was configured. Runs on every backup with no caching of a previous success,
    /// which is the only way to catch a destination that stopped being writable since the last one.
    /// Owns the one call that creates <paramref name="targetDir"/>, so a backup that has already
    /// failed a save-directory check never creates it.
    /// </summary>
    private static OperationResult? EnsureWritable(string backupRoot, string targetDir)
    {
        try
        {
            Directory.CreateDirectory(targetDir);

            // The .zip.tmp suffix means a probe left behind by a crash is picked up by the
            // existing stale-temp sweep and is already excluded from ListBackups, rather
            // than inventing a second cleanup mechanism for a second kind of leftover file.
            var probePath = Path.Combine(targetDir, $".probe-{Guid.NewGuid():N}.zip.tmp");
            File.WriteAllBytes(probePath, []);
            File.Delete(probePath);
            return null;
        }
        catch (Exception ex)
        {
            // Names backupRoot, not targetDir — that is the path the user configured and the one
            // they can act on.
            return OperationResult.Fail($"Backup folder not available: {backupRoot} ({ex.Message})");
        }
    }

    /// <summary>
    /// Deletes every leftover <c>*.zip.tmp</c> in this one game's backup folder before a new
    /// backup writes anything — never recursive, never a <c>backupRoot</c>-wide sweep. A
    /// startup sweep was considered and rejected: <c>backupRoot</c> may be unreachable at launch,
    /// and walking a possibly-network tree on every start to tidy a rare leftover is the wrong
    /// trade. Reports nothing, ever — housekeeping is not an event the user can act on, and
    /// a message about a file they never knew existed reads as a fault.
    /// </summary>
    private static void CleanStaleTempFiles(string targetDir)
    {
        List<string> stale;
        try
        {
            stale = [.. Directory.EnumerateFiles(targetDir, "*.zip.tmp")];
        }
        catch
        {
            return;
        }

        foreach (var file in stale)
        {
            try { File.Delete(file); }
            catch { /* best effort — a leftover .tmp is not fatal, the next run tries again */ }
        }
    }

    /// <summary>
    /// The SHA-256 of a stream, buffered with the same buffer size the copy uses. Hex-encoded;
    /// the comparison is case-insensitive everywhere it happens.
    /// </summary>
    private static string ComputeSha256(Stream stream)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[CopyBufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            hasher.AppendData(buffer, 0, read);
        return Convert.ToHexString(hasher.GetHashAndReset());
    }

    /// <summary>
    /// Compares a just-written archive against a fresh walk of its source directory. Returns
    /// <c>null</c> when they match, or a short detail sentence naming the first difference found —
    /// never throws. Exposed <c>internal</c> rather than <c>private</c> so tests can drive
    /// it directly.
    /// </summary>
    /// <param name="skipped">
    /// Paths deliberately absent from the archive — files the archive pass could not open (audit
    /// A2) and subtrees the walk could not read (audit A1). Removed from the expected set before
    /// comparison: those files are reported as a warning on the result, and re-demands them here
    /// would turn the skip policy back into a hard failure through verification's back door.
    /// Verification still fails for any OTHER mismatch — a file that vanished, shrank or grew.
    /// </param>
    /// <param name="expectedHashes">
    /// Optional entry-name-to-SHA-256 map recorded during the archive write. When present, each
    /// entry's decompressed content is re-hashed from the archive and compared — the content
    /// check that catches a file torn mid-write whose length happens to be intact (audit A3).
    /// Null keeps the name-and-length-only check.
    /// </param>
    /// <param name="verifyContent">
    /// Used by the restore path (which has no recorded hashes): when true, both the archive
    /// entry's decompressed content AND the corresponding file on disk are hashed and compared —
    /// proving the extraction wrote what the archive holds, not merely that lengths agree.
    /// Ignored when <paramref name="expectedHashes"/> is present.
    /// </param>
    internal static string? DescribeVerificationFailure(
        string archivePath, string sourceDir, IReadOnlyCollection<string>? excludedDirs = null,
        IReadOnlyCollection<string>? skipped = null,
        IReadOnlyDictionary<string, string>? expectedHashes = null, bool verifyContent = false)
    {
        Dictionary<string, long> sourceFiles;
        try
        {
            // Re-enumerated fresh rather than reusing a list captured before the write: a file
            // that changed during the backup must surface as a mismatch.
            sourceFiles = EnumerateRelativeFiles(sourceDir, excludedDirs);
        }
        catch (Exception ex)
        {
            return $"the save folder could not be re-read ({ex.Message})";
        }

        // Deliberately-skipped paths are excluded from the expected set — same comparer as the
        // dictionary itself, and DirectoryWalk reports skips relative to the walk root, which is
        // the same form EnumerateRelativeFiles keys. A skipped DIRECTORY has no file key: its
        // relative subtree prefix removes every file that lives under it.
        if (skipped is not null)
        {
            var skippedSet = new HashSet<string>(skipped, StringComparer.OrdinalIgnoreCase);
            var skippedDirs = skippedSet.Where(p => sourceFiles.Keys.All(k => !string.Equals(k, p, StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var key in sourceFiles.Keys
                .Where(k => skippedSet.Contains(k)
                         || skippedDirs.Any(d => k.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase)))
                .ToList())
                sourceFiles.Remove(key);
        }

        Dictionary<string, long> archiveFiles;
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var realEntries = zip.Entries
                // Directory placeholder entries are filtered unconditionally — whether
                // CreateFromDirectory emits one for a nested empty subdirectory is undocumented.
                .Where(e => !e.FullName.EndsWith('/'))
                .ToList();
            archiveFiles = realEntries.ToDictionary(e => e.FullName, e => e.Length, StringComparer.OrdinalIgnoreCase);

            // Content check (audit A3): a torn mid-write file whose length survived is invisible
            // to the name+length comparison, so each entry's decompressed stream is hashed. With
            // expectedHashes (backup path) the hash was recorded from the copy stream; with
            // verifyContent (restore path, which has no recorded hashes) the file on disk is
            // hashed too. Files with no expected hash — an archive from an older version of this
            // app, verified here through a call site that passes neither flag — keep the
            // name+length-only behaviour, so nothing about existing archives is broken.
            if (expectedHashes is not null || verifyContent)
            {
                foreach (var entry in realEntries)
                {
                    string entryHash;
                    try
                    {
                        using var entryStream = entry.Open();
                        entryHash = ComputeSha256(entryStream);
                    }
                    catch (Exception ex)
                    {
                        return $"\"{entry.FullName}\" could not be read from the archive ({ex.Message})";
                    }

                    if (expectedHashes is not null)
                    {
                        if (!expectedHashes.TryGetValue(entry.FullName, out var expected)
                            || !string.Equals(expected, entryHash, StringComparison.OrdinalIgnoreCase))
                            return $"\"{entry.FullName}\" content does not match the source file (hash mismatch)";
                    }
                    else if (verifyContent)
                    {
                        var fullPath = Path.Combine(sourceDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                        try
                        {
                            using var fileStream = File.OpenRead(fullPath);
                            if (!string.Equals(ComputeSha256(fileStream), entryHash, StringComparison.OrdinalIgnoreCase))
                                return $"\"{entry.FullName}\" content differs between the archive and the extracted copy (hash mismatch)";
                        }
                        catch (Exception ex)
                        {
                            return $"\"{entry.FullName}\" could not be read back from {sourceDir} ({ex.Message})";
                        }
                    }
                }
            }
        }
        catch (InvalidDataException ex)
        {
            return $"the archive could not be read back ({ex.Message})";
        }

        if (archiveFiles.Count != sourceFiles.Count)
            return $"archive has {archiveFiles.Count} entries, the save folder has {sourceFiles.Count}";

        foreach (var (relativePath, length) in sourceFiles)
        {
            if (!archiveFiles.TryGetValue(relativePath, out var entryLength))
                return $"\"{relativePath}\" is missing from the archive";

            if (entryLength != length)
                return $"\"{relativePath}\" is {entryLength} bytes in the archive but {length} bytes in the save folder";
        }

        return null;
    }

    /// <summary>
    /// Delegates the one-level-at-a-time walk to <see cref="DirectoryWalk.ForEachFile"/> — the
    /// same walk <see cref="SaveStatusScanner"/> uses — and projects each visited file into a
    /// relative-path-to-length entry.
    /// </summary>
    /// <param name="excludedDirs">
    /// Directories the walk steps over — the backup folder and the game's own archive folder,
    /// whenever the user placed the backup folder inside (or exactly at) the save folder being
    /// walked. A subtree is stepped over when it overlaps any excluded directory in either
    /// direction (it equals one, sits inside one, or contains one), not on an exact match alone:
    /// with <c>backupRoot</c> pointed at a save folder shared by several games, the other games'
    /// archive directories are subtrees of the walked save folder without equalling either
    /// exclusion, and only the containment rule keeps them out of the archive.
    /// </param>
    /// <param name="skipped">
    /// Optional collector for the paths this walk could not read, forwarded to
    /// <see cref="DirectoryWalk.ForEachFile"/>'s own skip reporter — the archive path uses it to
    /// surface an incomplete walk instead of silently verifying a possibly-incomplete archive.
    /// </param>
    private static Dictionary<string, long> EnumerateRelativeFiles(
        string rootDir, IReadOnlyCollection<string>? excludedDirs = null, ICollection<string>? skipped = null)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        DirectoryWalk.ForEachFile(rootDir, info =>
        {
            var relative = Path.GetRelativePath(rootDir, info.FullName).Replace(Path.DirectorySeparatorChar, '/');
            result[relative] = info.Length;
        }, excludedDirs, skipped is null ? null : skipped.Add);

        return result;
    }

    /// <summary>Reporting granularity inside a single file's copy — about one dispatcher
    /// post per 40 ms on a typical drive, roughly a thousand posts for a 1 GB save rather than the
    /// ~13,000 an unthrottled report per buffer read would produce.</summary>
    private const int ReportIntervalBytes = 1024 * 1024;

    /// <summary>Copy buffer size — the same default <see cref="Stream.CopyTo(Stream)"/>, and
    /// <c>ZipFileExtensions.CreateEntryFromFile</c>, already use.</summary>
    private const int CopyBufferSize = 81920;

    /// <summary>
    /// Writes <paramref name="saveDir"/> into a new zip at <paramref name="tempPath"/> using the
    /// same walk as <see cref="EnumerateRelativeFiles"/>, so verification cannot disagree with
    /// what was archived. Entry names use forward slashes.
    /// </summary>
    /// <param name="progress">
    /// Reports arrive at most once per <see cref="ReportIntervalBytes"/> inside a file, and always
    /// once more at each file boundary. This replaces an original once-per-file granularity: a
    /// save whose bytes sit in one or two large files (for example a 258.5 MB DayZ
    /// save) used to leave the bound row bar visibly static for the whole write, receiving only a
    /// report at the very start and one more when that dominant file finished. A reported byte is
    /// still a byte actually archived — every report follows the write that produced it. The
    /// walk's per-file length from <see cref="EnumerateRelativeFiles"/> is both the ceiling on the
    /// running total inside a file (so a file that grew since the walk cannot push the numerator
    /// past <see cref="ComputeSaveDataSize"/>'s precomputed maximum) and the floor reported at that
    /// file's boundary, so the final reported value still cannot disagree with
    /// <see cref="ComputeSaveDataSize"/>. One intentional consequence: a zero-byte file no longer
    /// emits its own boundary report, because its cumulative value did not move.
    /// </param>
    /// <param name="skipped">
    /// Optional collector forwarded to <see cref="EnumerateRelativeFiles"/> — paths the walk
    /// could not read, reported by the caller on the result so an incomplete archive is never
    /// mistaken for a complete one.
    /// </param>
    /// <returns>
    /// SHA-256 of every entry's uncompressed content, keyed by entry name — recorded for
    /// <see cref="DescribeVerificationFailure"/>'s content check, so a file torn mid-write that
    /// happens to keep its length can no longer pass verification (audit A3).
    /// </returns>
    private static Dictionary<string, string> ArchiveSaveDir(
        string saveDir, string tempPath, IProgress<long>? progress, IReadOnlyCollection<string>? excludedDirs = null,
        ICollection<string>? skipped = null)
    {
        var entryHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.Open(tempPath, ZipArchiveMode.Create);
        var buffer = new byte[CopyBufferSize];
        long completed = 0;
        long lastReported = 0;

        foreach (var (relative, length) in EnumerateRelativeFiles(saveDir, excludedDirs, skipped))
        {
            var fullPath = Path.Combine(saveDir, relative.Replace('/', Path.DirectorySeparatorChar));

            // Opened before the entry is created, matching ZipFileExtensions.CreateEntryFromFile's
            // own ordering: a file that cannot be opened fails here, before a dangling entry is
            // ever added to the archive. File.OpenRead's mode/access/share are the same ones that
            // helper used (FileShare.Read first, the write-compatible share), so a file another
            // process holds open behaves exactly as it does today — except that a file that
            // cannot be opened at all is now skipped and reported via <paramref name="skipped"/>
            // instead of aborting the whole backup (audit A2): one locked file must not cost the
            // user every other file in the archive. The path lands in the same warning the walk's
            // skipped subtrees produce, so the archive is never mistaken for complete.
            FileStream source;
            try
            {
                source = File.OpenRead(fullPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException
                or FileNotFoundException or DirectoryNotFoundException or PathTooLongException)
            {
                skipped?.Add(relative);
                continue;
            }

            using (source)
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);

                // Same clamp the BCL helper applies — the zip timestamp format cannot represent a
                // year outside 1980-2107.
                var lastWrite = File.GetLastWriteTime(fullPath);
                if (lastWrite.Year < 1980 || lastWrite.Year > 2107)
                    lastWrite = new DateTime(1980, 1, 1, 0, 0, 0);
                entry.LastWriteTime = lastWrite;

                using (var entryStream = entry.Open())
                {
                    long fileWritten = 0;
                    int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        entryStream.Write(buffer, 0, read);
                        hasher.AppendData(buffer, 0, read);
                        fileWritten += read;

                        // The walk's own length is the ceiling within this file — a file that grew
                        // under the walk cannot push the candidate past what the bar expects.
                        var candidate = completed + Math.Min(fileWritten, length);
                        if (candidate - lastReported >= ReportIntervalBytes)
                        {
                            progress?.Report(candidate);
                            lastReported = candidate;
                        }
                    }
                }

                // Recorded for verification: hashing during the copy stream costs one extra pass
                // of the hash algorithm only — no second read of the source file.
                entryHashes[relative] = Convert.ToHexString(hasher.GetHashAndReset());
            }

            // The per-file boundary report survives as the floor: even with no intra-file report
            // at all (a small file), the cumulative total still lands exactly on the walk's length.
            completed += length;
            if (completed != lastReported)
            {
                progress?.Report(completed);
                lastReported = completed;
            }
        }

        return entryHashes;
    }

    /// <summary>
    /// Sums <paramref name="game"/>'s save directory via the same <see cref="EnumerateRelativeFiles"/>
    /// walk <see cref="ArchiveSaveDir"/> archives from, so this number can never disagree with what
    /// actually gets archived — deliberately reusing that walk rather than a fresh one of its
    /// own. Deliberately passes NO excluded directories, unlike <see cref="ArchiveSaveDir"/>: it is
    /// a display-only denominator, so it may over-count when the backup folder sits inside (or IS)
    /// the save folder being summed — the walk the archive itself performs steps over those
    /// subtrees, and the archive can only ever be smaller than this figure, never larger. Wiring
    /// exclusions in here would need the per-game target folder this method does not know, for a
    /// number no decision is ever made from. Never throws across the service boundary — the same convention
    /// <see cref="IsBackupRootReachable"/> and <see cref="ListBackups"/> already follow — because
    /// its only consumers are advisory displays (the free-space line, the byte-progress bar's
    /// denominator) where a failure must be an absent number, not an error.
    /// </summary>
    public long ComputeSaveDataSize(Game game)
    {
        try
        {
            if (!PathResolver.TryResolve(game.SaveDirTemplate, null, out var saveDir, out _))
                return 0;
            if (!Directory.Exists(saveDir))
                return 0;
            return EnumerateRelativeFiles(saveDir).Values.Sum();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Reads <paramref name="backupRoot"/>'s available free space — a display-only
    /// probe, never a decision input. Returns false with <paramref name="freeBytes"/> at zero on
    /// any failure: an empty/whitespace root, a UNC path (per Microsoft's own docs, the
    /// <see cref="DriveInfo(string)"/> constructor does not support UNC paths — and this project's
    /// backup root is deliberately unconstrained by volume type, so a UNC destination is a real,
    /// reachable input), an unmounted drive letter, or anything else unreadable. False means
    /// "unknown", which the caller renders as an absent figure — never an error, a dialog, or a
    /// crash report, the same never-throw-across-the-boundary contract
    /// <see cref="IsBackupRootReachable"/> and <see cref="ListBackups"/> already follow. Creates and
    /// probes nothing: unlike <see cref="Backup(Game, string, string, out string?, IProgress{long}?)"/>'s
    /// own <see cref="EnsureWritable"/> check, merely showing a number must not touch the disk.
    /// </summary>
    public bool TryGetAvailableFreeSpace(string backupRoot, out long freeBytes)
    {
        freeBytes = 0;
        if (string.IsNullOrWhiteSpace(backupRoot))
            return false;

        try
        {
            freeBytes = new DriveInfo(backupRoot).AvailableFreeSpace;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Full path without a trailing separator, so two spellings of one folder compare equal.</summary>
    private static string NormalizeDir(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>
    /// Whether <paramref name="candidate"/> is <paramref name="parent"/> itself or sits anywhere
    /// beneath it. Restore asks this about the backup folder and a save folder it is about to
    /// replace wholesale.
    /// </summary>
    internal static bool IsSameOrInside(string candidate, string parent)
    {
        var child = NormalizeDir(candidate);
        var root = NormalizeDir(parent);
        return child.Equals(root, StringComparison.OrdinalIgnoreCase)
            || child.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Best-effort cleanup — a failed delete must never replace the original error.</summary>
    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best effort */ }
    }
}
