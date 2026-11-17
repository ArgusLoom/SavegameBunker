using System.Runtime.InteropServices;

namespace SavegameBunker.Services;

/// <summary>
/// Resolves Win32 known folders that have no <see cref="Environment.SpecialFolder"/> member.
/// </summary>
/// <remarks>
/// Saved Games is a user-relocatable known folder — Windows lets it be moved off the system
/// drive exactly like Documents can be moved by OneDrive Known Folder Move — but the BCL has no
/// <see cref="Environment.SpecialFolder"/> entry for it, so <c>SHGetKnownFolderPath</c> is the
/// only way to resolve it correctly. A hardcoded <c>%USERPROFILE%\Saved Games</c> would be wrong
/// on any machine where the folder was relocated, which is the same bug class the app already
/// fixed for Documents. Classic <c>[DllImport]</c> is used instead of <c>[LibraryImport]</c>
/// because this is a single one-off call and the self-contained publish does not require
/// trim/AOT-friendly marshalling — the extra ceremony <c>[LibraryImport]</c> needs for this
/// signature's <c>REFKNOWNFOLDERID</c>/<c>PWSTR*</c> shape buys nothing here.
/// </remarks>
internal static class KnownFolders
{
    private static readonly Guid FolderId_SavedGames = new("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    /// <summary>
    /// The current user's Saved Games folder, or null if it could not be resolved. Never throws.
    /// </summary>
    public static string? SavedGames
    {
        get
        {
            var hr = SHGetKnownFolderPath(FolderId_SavedGames, 0, IntPtr.Zero, out var ptr);
            if (hr != 0 || ptr == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                // The pointer is heap-allocated by the shell and must be freed by the caller
                // regardless of outcome, or every resolution leaks unmanaged memory.
                CoTaskMemFree(ptr);
            }
        }
    }
}
