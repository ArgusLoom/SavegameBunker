using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// A dialog fake that records what was shown instead of throwing on it, unlike
/// <see cref="ThrowingDialogService"/> — these tests deliberately expect <see cref="Info"/> and
/// <see cref="Error"/> to be called, and need to observe the view model's state at the moment they
/// are.
/// </summary>
public sealed class RecordingDialogService : IDialogService
{
    public List<string> InfoMessages { get; } = [];
    public List<string> ErrorMessages { get; } = [];
    public string? FolderToReturn { get; set; }
    public int PickFolderCalls { get; private set; }
    public List<string?> PickFolderInitialDirectories { get; } = [];

    /// <summary>
    /// Invoked at the start of both <see cref="Info"/> and <see cref="Error"/> — the deterministic
    /// observation point for the busy state. <see cref="MainViewModel.Report"/> runs after the
    /// backup returns but before <c>BackupAsync</c>'s <c>finally</c> resets
    /// <see cref="MainViewModel.IsBackupRunning"/>, so a hook here observes the window exactly as
    /// it is mid-backup with no timing assumption.
    /// </summary>
    public Action? OnReport { get; set; }

    public void Info(string message)
    {
        OnReport?.Invoke();
        InfoMessages.Add(message);
    }

    public void Error(string message)
    {
        OnReport?.Invoke();
        ErrorMessages.Add(message);
    }

    /// <summary>
    /// Invoked at the start of <see cref="PickFolder"/>, before anything is recorded — the
    /// deterministic observation point for what is enabled while a folder picker is open, the
    /// same idea as <see cref="OnReport"/>.
    /// </summary>
    public Action? OnPickFolder { get; set; }

    public bool ConfirmToReturn { get; set; }

    public Queue<bool>? ConfirmReturnSequence { get; set; }

    public List<string> ConfirmMessages { get; } = [];

    public List<string> ConfirmButtonTexts { get; } = [];

    public bool Confirm(string message, string confirmButtonText)
    {
        ConfirmMessages.Add(message);
        ConfirmButtonTexts.Add(confirmButtonText);
        if (ConfirmReturnSequence is { Count: > 0 })
            return ConfirmReturnSequence.Dequeue();
        return ConfirmToReturn;
    }

    public string? PickFolder(string title, string? initialDirectory)
    {
        OnPickFolder?.Invoke();
        PickFolderInitialDirectories.Add(initialDirectory);
        PickFolderCalls++;
        return FolderToReturn;
    }

    /// <summary>What <see cref="ShowAddGameDialog"/> returns when called — the seam the
    /// sorted-insert facts drive.</summary>
    public AddGameResult? AddGameToReturn { get; set; }

    /// <summary>Each <see cref="ShowAddGameDialog"/> call's <c>initialSaveFolder</c> argument — the
    /// seam the folder-drop tests use to prove a drop passed the path and the toolbar still passes null.</summary>
    public List<string?> ShowAddGameInitialFolders { get; } = [];

    public AddGameResult? ShowAddGameDialog(IReadOnlyCollection<string> existingIds, string? initialSaveFolder = null)
    {
        ShowAddGameInitialFolders.Add(initialSaveFolder);
        return AddGameToReturn;
    }

    /// <summary>What <see cref="ShowFindGamesDialog"/> returns when called — the seam that makes
    /// <c>FindGamesAsync</c>'s write path testable on a machine where no known-games template
    /// resolves.</summary>
    public IReadOnlyList<KnownGame>? FindGamesSelectionToReturn { get; set; }

    public List<ScanResult> FindGamesResults { get; } = [];

    public IReadOnlyList<KnownGame>? ShowFindGamesDialog(ScanResult result)
    {
        FindGamesResults.Add(result);
        return FindGamesSelectionToReturn;
    }

    public OperationResult OpenInEditor(string path) => OperationResult.Ok("opened");

    public List<string> OpenHttpsUrls { get; } = [];

    public OperationResult OpenHttps(string url)
    {
        OpenHttpsUrls.Add(url);
        return OperationResult.Ok("opened");
    }

    /// <summary>What <see cref="OpenHelpGuide"/> returns when called — the seam the help-command
    /// tests drive (success opens, failure reports).</summary>
    public OperationResult HelpGuideResultToReturn { get; set; } = OperationResult.Ok("opened");

    public List<string> HelpGuideLanguages { get; } = [];

    public OperationResult OpenHelpGuide(string uiLanguage)
    {
        HelpGuideLanguages.Add(uiLanguage);
        return HelpGuideResultToReturn;
    }

    /// <summary>What <see cref="ShowRestorePickerDialog"/> returns when called — the seam the
    /// restore flow tests drive.</summary>
    public BackupArchive? RestorePickerSelectionToReturn { get; set; }

    public List<IReadOnlyList<BackupArchive>> RestorePickerCalls { get; } = [];

    /// <summary>The delete action each <see cref="ShowRestorePickerDialog"/> call received — the
    /// seam that proves the caller supplies it, rather than the dialog layer constructing one.</summary>
    public List<Func<string, OperationResult>> RestorePickerDeleteActions { get; } = [];

    public BackupArchive? ShowRestorePickerDialog(string gameDisplayName, IReadOnlyList<BackupArchive> backups,
        Func<string, OperationResult> deleteArchive)
    {
        RestorePickerCalls.Add(backups);
        RestorePickerDeleteActions.Add(deleteArchive);
        return RestorePickerSelectionToReturn;
    }

    /// <summary>What <see cref="ShowChangeSaveFolderDialog"/> returns when called — the seam the
    /// repair-route tests drive.</summary>
    public string? ChangeSaveFolderToReturn { get; set; }

    public List<(string GameDisplayName, string CurrentTemplate)> ChangeSaveFolderCalls { get; } = [];

    public string? ShowChangeSaveFolderDialog(string gameDisplayName, string currentTemplate)
    {
        ChangeSaveFolderCalls.Add((gameDisplayName, currentTemplate));
        return ChangeSaveFolderToReturn;
    }
}
