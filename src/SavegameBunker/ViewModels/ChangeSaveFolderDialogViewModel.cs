using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SavegameBunker.Services;
using CommunityToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;

namespace SavegameBunker.ViewModels;

/// <summary>
/// Backs the Change Save Folder dialog — the in-window repair route for a row
/// whose save folder moved. Reuses <see cref="AddGameDialogViewModel"/>'s browse-and-validate path
/// for the one field this dialog edits.
/// </summary>
/// <remarks>
/// Identity (display name, id) is fixed and shown only as a label, never an editable field: the
/// id names the folder holding this game's backups, and this dialog must never be able to touch
/// it. There is no name box, no suggestion popup and no
/// validation line — unlike the Add game dialog, the confirm action here is blocked only by a
/// blank path, never by a folder that does not exist yet.
/// </remarks>
public sealed partial class ChangeSaveFolderDialogViewModel : CommunityToolkitObservableObject
{
    private readonly IDialogService _dialog;
    private int _resolveToken;

    public ChangeSaveFolderDialogViewModel(string gameDisplayName, string currentTemplate, IDialogService dialog)
    {
        GameDisplayName = gameDisplayName;
        Title = Strings.Format("str_changeSaveFolder_title", gameDisplayName);
        _dialog = dialog;
        SaveFolder = currentTemplate;
    }

    public string GameDisplayName { get; }

    public string Title { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    public partial string SaveFolder { get; set; } = "";

    partial void OnSaveFolderChanged(string value) => StartResolve(value);

    /// <summary>
    /// What the entered <see cref="SaveFolder"/> template resolves to — a file count and
    /// size, "Checking…" while the walk is in flight, or why it could not be resolved. Never gates
    /// <see cref="CanConfirm"/>: a folder that does not exist yet is not an error here either.
    /// </summary>
    [ObservableProperty]
    public partial string ResolvedStatus { get; private set; } = "";

    /// <summary>
    /// True once the path is non-blank — the only gate this dialog has ("the confirm
    /// button is never blocked merely because the folder does not exist yet").
    /// </summary>
    public bool CanConfirm => !string.IsNullOrWhiteSpace(SaveFolder);

    [RelayCommand]
    private Task BrowseAsync()
    {
        var picked = _dialog.PickFolder(Strings.Get("str_common_browseFolderTitle"), null);
        if (picked is not null)
            SaveFolder = PathResolver.ToTemplate(picked);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Kicks off the resolve-and-report walk for <paramref name="folder"/> off the UI thread. A
    /// monotonically increasing token guards against a stale resolve (started for a folder the
    /// user has since changed) overwriting a newer one's result — the same shape
    /// <see cref="AddGameDialogViewModel"/> uses for the identical reason.
    /// </summary>
    private void StartResolve(string folder)
    {
        var token = ++_resolveToken;
        ResolvedStatus = Strings.Get("str_common_checking");

        _ = ReportResolveAsync(folder, token);
    }

    private async Task ReportResolveAsync(string folder, int token)
    {
        var result = await Task.Run(() => ResolveFolder(folder));

        if (token == _resolveToken)
            ResolvedStatus = result;
    }

    private static string ResolveFolder(string folder)
    {
        if (!PathResolver.TryResolve(folder, null, out var dir, out var error))
            return error!;

        if (!Directory.Exists(dir))
            return Strings.Get("str_common_folderNotFound");

        var (count, bytes) = CountFiles(dir);
        return count == 1
            ? Strings.Format("str_common_fileCountOne", SizeFormat.Describe(bytes))
            : Strings.Format("str_common_fileCount", count, SizeFormat.Describe(bytes));
    }

    /// <summary>
    /// Walks the tree one directory level at a time, catching per level so a locked or vanished
    /// subfolder yields a partial count instead of aborting the whole report — the same shape
    /// <see cref="AddGameDialogViewModel.CountFiles"/> uses for the identical reason.
    /// </summary>
    private static (int Count, long Bytes) CountFiles(string root)
    {
        var count = 0;
        var bytes = 0L;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    // A symlinked file is skipped, matching BackupService — the archive never
                    // contains it, so the preview must not count it either. A reparse-flagged file
                    // that is not an actual link (e.g. a OneDrive Files On-Demand placeholder)
                    // still counts.
                    FileInfo info;
                    try
                    {
                        info = new FileInfo(file);
                        if (ReparseFile.IsSymlink(info))
                            continue;
                    }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (IOException) { continue; }

                    count++;
                    bytes += info.Length;
                }

                foreach (var subDir in Directory.EnumerateDirectories(dir))
                {
                    // A junction or symbolic link back up the tree would otherwise make this
                    // walk run forever — same guard SaveStatusScanner applies for the identical
                    // reason.
                    try
                    {
                        if (File.GetAttributes(subDir).HasFlag(FileAttributes.ReparsePoint))
                            continue;
                    }
                    catch (UnauthorizedAccessException) { continue; }
                    catch (IOException) { continue; }

                    pending.Push(subDir);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                // A locked or vanished subfolder must not abort the walk — the report is a
                // best-effort count, not a promise every byte was seen.
            }
        }

        return (count, bytes);
    }
}
