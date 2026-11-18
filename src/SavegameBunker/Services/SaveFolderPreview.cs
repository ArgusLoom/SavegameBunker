using System.IO;

namespace SavegameBunker.Services;

/// <summary>
/// The resolve line the Add game and Change save folder dialogs both show, kept in one place so
/// the two cannot drift apart (ITEM-16).
/// </summary>
/// <remarks>
/// Pure apart from the read-only folder walk: <see cref="PathResolver.TryResolve"/>,
/// <see cref="Directory.Exists"/> and <see cref="DirectoryWalk.CountFiles"/> all report instead of
/// throwing, so <see cref="Describe"/> never throws. Callers run it off the UI thread.
/// Internal, so ServiceContractTests' public-surface fact is unaffected.
/// </remarks>
internal static class SaveFolderPreview
{
    internal static string Describe(string folder)
    {
        if (!PathResolver.TryResolve(folder, null, out var dir, out var error))
            return error!;

        if (!Directory.Exists(dir))
            return Strings.Get("str_common_folderNotFound");

        var (count, bytes) = DirectoryWalk.CountFiles(dir);
        return count == 1
            ? Strings.Format("str_common_fileCountOne", SizeFormat.Describe(bytes))
            : Strings.Format("str_common_fileCount", count, SizeFormat.Describe(bytes));
    }
}
