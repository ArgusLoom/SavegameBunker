using System.IO;

namespace SavegameBunker.Models;

/// <summary>
/// One archive on disk, as returned by <see cref="Services.BackupService.ListBackups"/>. This is
/// the public seam three separate features bind to: the restore picker's row list, the
/// save-vs-backup comparison's verdict, and the safety backup taken before a restore.
/// </summary>
/// <param name="FilePath">Absolute path of the <c>.zip</c> file.</param>
/// <param name="TakenAt">
/// When the backup was taken, parsed from the file name where possible and falling back to the
/// file's last-write time otherwise.
/// </param>
/// <param name="SizeBytes">
/// The archive file's own size on disk — not the uncompressed size of its contents, which is
/// otherwise ambiguous next to a timestamp in a restore picker row.
/// </param>
public sealed record BackupArchive(string FilePath, DateTime TakenAt, long SizeBytes)
{
    /// <summary>
    /// True when this archive is one of restore's own safety backups (the <c>-pre-restore</c>
    /// file-name suffix, optionally followed by <see cref="Services.BackupService"/>'s own
    /// same-second collision suffix, e.g. <c>-pre-restore-1</c>). Shared by the restore picker tag
    /// and bulk restore's newest-ordinary pick so those two cannot disagree about what a safety
    /// archive is.
    /// </summary>
    public bool IsSafetyBackup
    {
        get
        {
            var name = Path.GetFileNameWithoutExtension(FilePath);
            // The collision suffix is stripped only on the exact shape the app writes, so a
            // hand-renamed "before-pre-restore-2024" stays an ordinary archive.
            return name.EndsWith("-pre-restore", StringComparison.Ordinal)
                || name.EndsWith("-pre-restore-partial", StringComparison.Ordinal)
                || SafetyWithCollisionSuffix.IsMatch(name);
        }
    }

    /// <summary>
    /// True when the backup that wrote this archive could not read every file (its name carries
    /// <c>-partial</c>, optionally followed by the collision suffix). Such an archive is still
    /// listed and restorable by hand, but never counts as the copy that makes a game "up to date"
    /// and is never picked automatically by bulk restore.
    /// </summary>
    public bool IsPartial
    {
        get
        {
            var name = Path.GetFileNameWithoutExtension(FilePath);
            return name.EndsWith("-partial", StringComparison.Ordinal) || PartialWithCollisionSuffix.IsMatch(name);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex PartialWithCollisionSuffix = new(
        @"^\d{4}-\d{2}-\d{2}_\d{6}(-pre-restore)?-partial-\d+$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex SafetyWithCollisionSuffix = new(
        @"^\d{4}-\d{2}-\d{2}_\d{6}-pre-restore(-partial)?-\d+$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
