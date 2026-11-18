using System.IO;

namespace SavegameBunker.Services;

/// <summary>
/// Computes a starting folder for the backup folder picker — nothing else. The backup
/// destination itself is always whatever the user picks in that dialog; this class never feeds
/// <c>StoreBackupRootAsync</c>, <c>_load.Catalog.BackupRoot</c> or <see cref="BackupService.Backup(Models.Game, string, IProgress{long}?)"/>
/// directly or indirectly.
/// </summary>
/// <remarks>
/// SPEC.md's *Backup destination* section permits exactly this one use of volume type — "used only to pick
/// a sensible starting folder in the picker" — and forbids every other use: no code path may
/// refuse, redirect or relocate a backup based on where it points. A future reader must not mistake
/// this for an eligibility rule; the folder the user chooses is honoured unchanged regardless of
/// what this class returns.
/// </remarks>
internal static class DriveHints
{
    /// <summary>
    /// The pure selection rule, given a snapshot of the machine's volumes and its system root.
    /// Takes a tuple sequence rather than <see cref="DriveInfo"/> because <see cref="DriveInfo"/>
    /// cannot be constructed for a non-existent drive in a test.
    /// </summary>
    /// <remarks>
    /// <see cref="DriveType.Network"/> never qualifies: <see cref="DriveInfo.IsReady"/> on a stale
    /// mapped network drive blocks for the SMB timeout, and this call sits directly in front of a
    /// picker the user is waiting on. A share is also not a volume the user can carry away — the
    /// picker still lets the user browse to any share, only the starting folder differs.
    /// </remarks>
    internal static string? ChooseStartFolder(IEnumerable<(string Root, DriveType Type, bool IsReady)> volumes, string systemRoot)
    {
        var qualifying = volumes.Where(v =>
            v.IsReady &&
            (v.Type == DriveType.Removable ||
                (v.Type == DriveType.Fixed && !string.Equals(v.Root, systemRoot, StringComparison.OrdinalIgnoreCase))));

        var ordered = qualifying
            .OrderBy(v => v.Type == DriveType.Removable ? 0 : 1)
            .ThenBy(v => v.Root, StringComparer.OrdinalIgnoreCase);

        return ordered.FirstOrDefault().Root;
    }

    /// <summary>
    /// Whether <see cref="DriveInfo.IsReady"/> may be read for a volume of the given type.
    /// IsReady on a stale mapped network drive blocks for the SMB timeout and is slow on optical
    /// drives, and only Fixed and Removable volumes can ever be chosen by
    /// <see cref="ChooseStartFolder"/>, so the readiness probe is skipped for every other type.
    /// </summary>
    internal static bool ShouldProbeReadiness(DriveType type)
    {
        return type == DriveType.Fixed || type == DriveType.Removable;
    }

    /// <summary>
    /// Enumerates the machine's drives and hands them to <see cref="ChooseStartFolder"/>. Never
    /// throws across its boundary — same never-throw-across-the-boundary contract every other
    /// service in <c>Services/</c> holds — because a failure here must never block the picker.
    /// </summary>
    internal static string? SuggestBackupStartFolder()
    {
        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (string.IsNullOrWhiteSpace(systemRoot))
            return null;

        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception)
        {
            return null;
        }

        var volumes = new List<(string Root, DriveType Type, bool IsReady)>();
        foreach (var drive in drives)
        {
            try
            {
                var type = drive.DriveType;
                volumes.Add((drive.RootDirectory.FullName, type, ShouldProbeReadiness(type) && drive.IsReady));
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
        }

        return ChooseStartFolder(volumes, systemRoot);
    }
}
