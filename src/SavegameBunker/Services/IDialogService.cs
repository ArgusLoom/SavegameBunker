using System.IO;
using SavegameBunker.Models;

namespace SavegameBunker.Services;

/// <summary>
/// Abstraction over user-facing dialogs so view models stay testable and
/// free of direct WPF/MessageBox dependencies.
/// </summary>
public interface IDialogService
{
    void Info(string message);
    void Error(string message);
    /// <summary>Asks a yes/no question. <paramref name="confirmButtonText"/> names the action on
    /// the confirming button ("Restore", "Remove") so the button says what it will do; the
    /// other button is always Cancel.</summary>
    bool Confirm(string message, string confirmButtonText);

    /// <summary>Opens a folder picker. Returns the chosen folder, or null if cancelled.</summary>
    string? PickFolder(string title, string? initialDirectory);

    /// <summary>
    /// Opens the Add Game dialog. Returns the confirmed entry, or null if the user cancelled —
    /// a cancelled dialog writes nothing and is not a failure. When
    /// <paramref name="initialSaveFolder"/> is a non-blank path it is written into the save-folder
    /// field before show; the display name stays empty.
    /// </summary>
    AddGameResult? ShowAddGameDialog(IReadOnlyCollection<string> existingIds, string? initialSaveFolder = null);

    /// <summary>
    /// Opens the Find Games window over <paramref name="result"/>. Returns the games the user
    /// picked to add, or null if the user cancelled — a cancelled scan writes nothing.
    /// </summary>
    IReadOnlyList<KnownGame>? ShowFindGamesDialog(ScanResult result);

    /// <summary>
    /// Opens <paramref name="path"/> in whatever application Windows has associated with it,
    /// falling back to Explorer with the file selected when nothing is associated — the
    /// user always lands somewhere useful. Directories and `.json` files are allowed; everything
    /// else, including a missing path, is refused (see <see cref="ShellOpen"/>) so a command-line
    /// path or a hand-edited catalog cannot launch a program via Open.
    /// </summary>
    OperationResult OpenInEditor(string path);

    /// <summary>
    /// Opens an https URL in the default browser. Only URLs that start with
    /// <c>https://www.pcgamingwiki.com/</c> are allowed; everything else is refused without
    /// starting a process so this cannot become a general shell-out.
    /// </summary>
    OperationResult OpenHttps(string url);

    /// <summary>
    /// Opens the offline user guide shipped as <c>help/&lt;lang&gt;/</c> beside the executable
    /// (build the site, the app only reads it). <paramref name="uiLanguage"/> is the UI culture's
    /// two-letter code; German opens <c>help/de/</c>, anything else <c>help/en/</c>, falling back
    /// to English when the preferred site is absent. When neither site is present the guide is
    /// not installed — that is reported, not opened (see <see cref="HelpPath"/>). Unlike
    /// <see cref="OpenInEditor"/>, this resolves the path itself from a trusted base directory,
    /// so no user-editable path reaches the shell.
    /// </summary>
    OperationResult OpenHelpGuide(string uiLanguage);

    /// <summary>
    /// Opens the restore picker over <paramref name="backups"/>, titled with
    /// <paramref name="gameDisplayName"/> since the window is opened per-row. Returns the picked
    /// archive, or null if the user cancelled — a cancelled picker restores nothing.
    /// </summary>
    BackupArchive? ShowRestorePickerDialog(string gameDisplayName, IReadOnlyList<BackupArchive> backups);

    /// <summary>
    /// Opens the Change Save Folder dialog for one row, pre-filled with
    /// <paramref name="currentTemplate"/>. Identity (<paramref name="gameDisplayName"/>, the id) is
    /// fixed and not editable here — only the save-folder template can change. Returns the new
    /// template, or null if the user cancelled — a cancelled dialog writes nothing.
    /// </summary>
    string? ShowChangeSaveFolderDialog(string gameDisplayName, string currentTemplate);
}
