using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SavegameBunker.Tests;

/// <summary>
/// Filesystem manipulation helpers shared by any test that needs to simulate a locked subfolder
/// or a directory junction — moved out of <see cref="SaveStatusScannerTests"/> once
/// <see cref="BackupServiceTests"/> needed the same two capabilities, so there is one home for
/// them rather than two copies drifting apart.
/// </summary>
internal static class TestFileSystem
{
    /// <summary>
    /// Applies a Deny ACE for the current user on <paramref name="path"/>'s list/read rights, so
    /// enumerating it throws <see cref="UnauthorizedAccessException"/> the same way a genuinely
    /// locked subfolder would. Returns false (caller then no-ops) if the current identity or
    /// filesystem does not support setting an ACL, rather than failing the whole run on an
    /// environment this cannot be guaranteed on.
    /// </summary>
    public static bool TryDenyAccess(string path)
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent().User;
            if (identity is null)
                return false;

            var info = new DirectoryInfo(path);
            var security = info.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(
                identity,
                FileSystemRights.ListDirectory | FileSystemRights.Read,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Deny));
            info.SetAccessControl(security);

            // Confirm the ACL actually blocks enumeration on this machine before trusting it.
            try
            {
                Directory.EnumerateFiles(path).ToList();
                return false; // Deny rule did not take effect (e.g. elevated/bypass context).
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// A denied-ACL subdirectory must be re-permitted level by level, before it is possible to
    /// even list its own contents to recurse further.
    /// </summary>
    public static void RemoveDenyRules(string dir)
    {
        try
        {
            var info = new DirectoryInfo(dir);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return;

            var security = info.GetAccessControl();
            security.SetAccessRuleProtection(false, false);
            foreach (var rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier))
                         .Cast<FileSystemAccessRule>()
                         .Where(r => r.AccessControlType == AccessControlType.Deny))
                security.RemoveAccessRule(rule);
            info.SetAccessControl(security);
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        string[] subdirs;
        try
        {
            subdirs = Directory.GetDirectories(dir);
        }
        catch (UnauthorizedAccessException) { return; }

        foreach (var sub in subdirs)
            RemoveDenyRules(sub);
    }

    /// <summary>
    /// Creates a directory junction via <c>mklink /J</c> — junctions need no elevated privilege on
    /// NTFS, unlike <see cref="Directory.CreateSymbolicLink(string, string)"/>. Returns false
    /// (caller then no-ops) if junction creation is unavailable in this environment.
    /// </summary>
    public static bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            process!.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(linkPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a file symbolic link via <see cref="File.CreateSymbolicLink(string, string)"/>.
    /// Unlike a directory junction, this needs either an elevated process or Developer Mode
    /// enabled on the machine. Returns false (caller then no-ops) if link creation is unavailable
    /// in this environment.
    /// </summary>
    public static bool TryCreateFileSymlink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return File.Exists(linkPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// A junction must be unlinked directly (non-recursive delete) rather than descended into, or
    /// a recursive delete tries to walk back into the tree it points at.
    /// </summary>
    public static void RemoveJunctions(string dir)
    {
        string[] subdirs;
        try
        {
            subdirs = Directory.GetDirectories(dir);
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        foreach (var sub in subdirs)
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(sub);
            }
            catch (IOException) { continue; }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                try { Directory.Delete(sub, recursive: false); }
                catch (IOException) { }
                continue;
            }

            RemoveJunctions(sub);
        }
    }
}
