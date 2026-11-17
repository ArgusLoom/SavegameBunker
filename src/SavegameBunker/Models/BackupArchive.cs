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
    /// file-name suffix). Shared by the restore picker tag and bulk restore's newest-ordinary
    /// pick so those two cannot disagree about what a safety archive is.
    /// </summary>
    public bool IsSafetyBackup =>
        Path.GetFileNameWithoutExtension(FilePath).EndsWith("-pre-restore", StringComparison.Ordinal);
}
