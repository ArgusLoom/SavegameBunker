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
    private void Browse()
    {
        var picked = _dialog.PickFolder(Strings.Get("str_common_browseFolderTitle"), null);
        if (picked is not null)
            SaveFolder = PathResolver.ToTemplate(picked);
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
        var result = await Task.Run(() => SaveFolderPreview.Describe(folder));

        if (token == _resolveToken)
            ResolvedStatus = result;
    }
}
