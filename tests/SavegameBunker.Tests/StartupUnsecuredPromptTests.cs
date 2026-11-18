using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Proves the startup prompt for unsecured games: which rows count as
/// unsecured, and how the dialog's answer routes — Yes re-enters the existing bulk backup flow,
/// No does nothing, and N = 0 never reaches a dialog. The prompt is driven by arming it with
/// <see cref="MainViewModel.RequestStartupUnsecuredPrompt"/> — which evaluates at once when the
/// first full sweep has already settled — and otherwise by the first full sweep that settles,
/// <see cref="MainViewModel.PromptUnsecuredGamesOnce"/> being the single evaluation point inside.
/// </summary>
public sealed class StartupUnsecuredPromptTests : IDisposable
{
    private readonly string _dir;

    public StartupUnsecuredPromptTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    /// <summary>
    /// Builds a view model over <paramref name="gameCount"/> games, each with a real, non-empty
    /// save directory and a real, reachable backup root beside them, then waits for the
    /// construction-time sweep to settle so the tests can force their own comparison kinds
    /// afterwards — the same synthetic-state technique <see cref="MainViewModelTests"/> uses.
    /// Waiting also guarantees the first full sweep has marked the settled flag, so each test's
    /// arming evaluates at once (the way App does when ReportLoad's probe outlasts the sweep)
    /// and is never raced by the sweep's own completion.
    /// </summary>
    private Task<(MainViewModel Vm, RecordingDialogService Dialog, string BackupRoot)> CreateViewModelAsync(int gameCount) =>
        CreateViewModelAsync(gameCount, configuredBackupRoot: null);

    /// <param name="configuredBackupRoot">
    /// The catalog's <c>backupRoot</c>; <c>null</c> (the default) seeds the usual real root beside
    /// the save directories. An empty string leaves the root unset, so every row settles on
    /// <see cref="BackupComparisonKind.FolderNotAvailable"/> and the prompt has nothing to offer —
    /// the picker route the unset root would take is covered by the bulk-path fact in
    /// <see cref="MainViewModelTests"/> (<c>Back_up_selected_with_an_unset_backup_root_…</c>),
    /// because with an unset root no row ever qualifies for the startup prompt.
    /// </param>
    private async Task<(MainViewModel Vm, RecordingDialogService Dialog, string BackupRoot)> CreateViewModelAsync(
        int gameCount, string? configuredBackupRoot)
    {
        var backupRoot = Path.Combine(_dir, "backups");
        var games = new List<Game>();
        for (var i = 0; i < gameCount; i++)
        {
            var saveDir = Path.Combine(_dir, $"saves-{i}");
            Directory.CreateDirectory(saveDir);
            File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
            games.Add(new Game { Id = $"game-{i}", DisplayName = $"Game {i}", SaveDirTemplate = saveDir.Replace('\\', '/') });
        }

        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog { BackupRoot = configuredBackupRoot ?? backupRoot, Games = games };
        await GameCatalog.SaveAsync(catalogPath, catalog);

        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);

        await WaitForAsync(() => vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));
        await WaitForAsync(() => vm.IsFirstFullSweepSettled);

        return (vm, dialog, backupRoot);
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        Assert.True(condition(), "Timed out waiting for the expected view model state.");
    }

    [Fact]
    public async Task Unarmed_prompt_shows_no_dialog_even_with_unsecured_rows()
    {
        var (vm, dialog, _) = await CreateViewModelAsync(1);

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        Assert.Single(vm.GetUnsecuredRows());

        vm.PromptUnsecuredGamesOnce();

        Assert.Empty(dialog.ConfirmMessages);
    }

    [Fact]
    public async Task No_unsecured_games_means_no_dialog()
    {
        var (vm, dialog, _) = await CreateViewModelAsync(2);

        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[1].ApplyBackupComparison(BackupComparison.NoSaveData(DateTime.Now));

        Assert.Empty(vm.GetUnsecuredRows());
        vm.RequestStartupUnsecuredPrompt();

        Assert.Empty(dialog.ConfirmMessages);
    }

    [Fact]
    public async Task Declining_the_prompt_starts_no_backup()
    {
        var (vm, dialog, backupRoot) = await CreateViewModelAsync(1);
        dialog.ConfirmToReturn = false;

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.RequestStartupUnsecuredPrompt();
        await WaitForAsync(() => dialog.ConfirmMessages.Count == 1);

        // N = 1 must name the game — the singular resource carries a {0} placeholder, so the
        // display name survives the format. Asserted on content, not on the exact sentence.
        Assert.Contains("Game 0", dialog.ConfirmMessages[0]);
        // Content fragment, not the full label — the intent is that the prompt's confirming
        // button names the action, not that the wording is byte-stable.
        Assert.Contains("back up now", dialog.ConfirmButtonTexts[0], StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-0")));
        Assert.Equal(0, dialog.PickFolderCalls);
        Assert.False(vm.IsBulkBackupRunning);
    }

    [Fact]
    public async Task Accepting_the_prompt_runs_the_bulk_backup_for_the_unsecured_games()
    {
        var (vm, dialog, backupRoot) = await CreateViewModelAsync(2);
        dialog.ConfirmToReturn = true;

        vm.Games[0].ApplyBackupComparison(BackupComparison.UpToDate(DateTime.Now));
        vm.Games[1].ApplyBackupComparison(BackupComparison.SavesChangedSinceBackup(DateTime.Now.AddDays(-1), TimeSpan.FromDays(1)));

        vm.RequestStartupUnsecuredPrompt();

        // The bulk run re-derives its targets from the same predicate, so only the unsecured row
        // gets an archive — the up-to-date row must stay untouched.
        await WaitForAsync(() => Directory.Exists(Path.Combine(backupRoot, "game-1")));
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-0")));
        Assert.Single(dialog.ConfirmMessages);
        // A configured backup root means EnsureBackupRootAsync never opens the picker.
        Assert.Equal(0, dialog.PickFolderCalls);
    }

    /// <summary>
    /// The prompt's Yes routes through <see cref="MainViewModel.BackupAllChangedCommand"/>, not
    /// around it: with the command disabled (a row busy gates <c>CanBackupAllChanged</c> exactly
    /// as it gates the toolbar button), answering Yes must start no backup — the same no-op a
    /// disabled toolbar button is.
    /// </summary>
    [Fact]
    public async Task Yes_does_not_start_the_backup_when_the_bulk_command_is_disabled()
    {
        var (vm, dialog, backupRoot) = await CreateViewModelAsync(2);
        dialog.ConfirmToReturn = true;

        // IsBusy on any row is one of CanBackupAllChanged's gate terms, and it survives here: the
        // construction-time sweep already settled (CreateViewModelAsync waited for it), so nothing
        // clears it underneath the test.
        vm.Games[0].IsBusy = true;
        vm.Games[1].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        Assert.False(vm.BackupAllChangedCommand.CanExecute(null));

        vm.RequestStartupUnsecuredPrompt();
        await WaitForAsync(() => dialog.ConfirmMessages.Count == 1);

        // Long enough for the pre-fix direct-call path to have written an archive; none may appear.
        await Task.Delay(300);

        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-1")));
        Assert.False(vm.IsBulkBackupRunning);
    }

    [Fact]
    public async Task Prompt_is_evaluated_at_most_once_per_session()
    {
        var (vm, dialog, _) = await CreateViewModelAsync(2);
        dialog.ConfirmToReturn = false;

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.RequestStartupUnsecuredPrompt();
        await WaitForAsync(() => dialog.ConfirmMessages.Count == 1);

        // A second game becomes unsecured afterwards; the session has already had its one prompt,
        // so a second arming evaluates to nothing.
        vm.Games[1].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm.RequestStartupUnsecuredPrompt();
        vm.PromptUnsecuredGamesOnce();

        Assert.Single(dialog.ConfirmMessages);
    }

    /// <summary>
    /// An <see cref="IDialogService"/> whose <see cref="IDialogService.Confirm"/> throws, delegating
    /// everything else to a <see cref="RecordingDialogService"/> so the failure's side effects stay
    /// observable — the seam <see cref="MainViewModel.PromptUnsecuredGamesOnce"/>'s catch is
    /// written against.
    /// </summary>
    private sealed class ThrowingConfirmDialogService : IDialogService
    {
        private readonly RecordingDialogService _inner = new();

        public List<string> ErrorMessages => _inner.ErrorMessages;
        public List<string> ConfirmMessages => _inner.ConfirmMessages;

        // The dialog request itself reaches the dialog system before its backend dies, so the
        // attempt is recorded first and the throw comes second. That keeps ConfirmMessages a
        // faithful count of attempted prompts — exactly one here — which is what proves the
        // one-shot flags consume the shot even when the Confirm throws.
        public bool Confirm(string message, string confirmButtonText)
        {
            _inner.Confirm(message, confirmButtonText);
            throw new InvalidOperationException("dialog backend died");
        }

        public void Info(string message) => _inner.Info(message);
        public void Error(string message) => _inner.Error(message);
        public string? PickFolder(string title, string? initialDirectory) => _inner.PickFolder(title, initialDirectory);
        public AddGameResult? ShowAddGameDialog(IReadOnlyCollection<string> existingIds, string? initialSaveFolder = null) =>
            _inner.ShowAddGameDialog(existingIds, initialSaveFolder);
        public IReadOnlyList<KnownGame>? ShowFindGamesDialog(ScanResult result) => _inner.ShowFindGamesDialog(result);
        public OperationResult OpenInEditor(string path) => _inner.OpenInEditor(path);
        public OperationResult OpenHttps(string url) => _inner.OpenHttps(url);
        public OperationResult OpenHelpGuide(string uiLanguage) => _inner.OpenHelpGuide(uiLanguage);
        public BackupArchive? ShowRestorePickerDialog(string gameDisplayName, IReadOnlyList<BackupArchive> backups,
            Func<string, OperationResult> deleteArchive) =>
            _inner.ShowRestorePickerDialog(gameDisplayName, backups, deleteArchive);
        public string? ShowChangeSaveFolderDialog(string gameDisplayName, string currentTemplate) =>
            _inner.ShowChangeSaveFolderDialog(gameDisplayName, currentTemplate);
    }

    [Fact]
    public async Task A_throwing_Confirm_reports_the_failure_and_keeps_the_app_alive()
    {
        var (_, _, backupRoot) = await CreateViewModelAsync(1);
        var dialogs = new ThrowingConfirmDialogService();
        var vm2 = new MainViewModel(await GameCatalog.LoadAsync(Path.Combine(_dir, "games.json")), dialogs);
        await WaitForAsync(() => vm2.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));
        await WaitForAsync(() => vm2.IsFirstFullSweepSettled);

        vm2.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        vm2.RequestStartupUnsecuredPrompt();

        // The exception surfaces through the app's normal error path, not as a crash.
        await WaitForAsync(() => dialogs.ErrorMessages.Count == 1);
        Assert.Contains("dialog backend died", dialogs.ErrorMessages[0]);

        // The app state stays consistent: the shot is consumed, so nothing re-prompts, and no
        // backup was started by the dead prompt.
        Assert.Single(dialogs.ConfirmMessages);
        vm2.PromptUnsecuredGamesOnce();
        await Task.Delay(50);
        Assert.Single(dialogs.ConfirmMessages);
        Assert.Single(dialogs.ErrorMessages);
        Assert.False(Directory.Exists(Path.Combine(backupRoot, "game-0")));
        Assert.False(vm2.IsBulkBackupRunning);
    }

    /// <summary>
    /// Arming can happen after the first full sweep already settled — ReportLoad's reachability
    /// probe can outlast the sweep — so arming itself must evaluate the prompt at once. Only
    /// <see cref="MainViewModel.RequestStartupUnsecuredPrompt"/> is called: the prompt must come
    /// from the arming evaluation, never from another sweep settling afterwards.
    /// </summary>
    [Fact]
    public async Task Arming_after_the_first_full_sweep_settled_prompts_without_another_sweep()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "backups"));
        var (vm, dialog, _) = await CreateViewModelAsync(1);

        vm.Games[0].ApplyBackupComparison(BackupComparison.NeverBackedUp());
        await WaitForAsync(() => vm.IsFirstFullSweepSettled);

        vm.RequestStartupUnsecuredPrompt();

        await WaitForAsync(() => dialog.ConfirmMessages.Count == 1);
        Assert.Contains("Game 0", dialog.ConfirmMessages[0]);
    }

    /// <summary>
    /// A full sweep over zero rows also counts as settled, so an empty catalog consumes an armed
    /// prompt on the spot instead of saving it up — a later Refresh after a game was added must
    /// never show the startup prompt mid-session. The negative proof waits past the point where
    /// the Refresh's full sweep settles and would asynchronously have shown the dialog.
    /// </summary>
    [Fact]
    public async Task An_empty_catalogs_first_sweep_consumes_the_armed_prompt_so_a_later_refresh_never_shows_it()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "backups"));
        var (vm, dialog, backupRoot) = await CreateViewModelAsync(0);
        await WaitForAsync(() => vm.IsFirstFullSweepSettled);
        vm.RequestStartupUnsecuredPrompt();

        var saveDir = Path.Combine(_dir, "saves-new");
        Directory.CreateDirectory(saveDir);
        File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
        var catalog = new Catalog
        {
            BackupRoot = backupRoot,
            Games = [new Game { Id = "game-new", DisplayName = "Game new", SaveDirTemplate = saveDir.Replace('\\', '/') }],
        };
        await GameCatalog.SaveAsync(Path.Combine(_dir, "games.json"), catalog);

        vm.RefreshCommand.Execute(null);
        await WaitForAsync(() =>
            vm.Games.Count == 1 && vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));
        await Task.Delay(300);

        Assert.Empty(dialog.ConfirmMessages);
    }

    /// <summary>
    /// The prompt can only ever offer rows whose comparison says their saves are uncovered —
    /// <see cref="BulkRunRules.NeedsBackup"/> accepts only SavesChangedSinceBackup and
    /// NeverBackedUp. With an unset backup root every row settles on FolderNotAvailable, so
    /// nothing is unsecured and the prompt is a synchronous no-op: the picker route an unset root
    /// would take is unreachable from here and covered by the bulk-path fact in
    /// <see cref="MainViewModelTests"/> instead (D-04, D-13).
    /// </summary>
    [Fact]
    public async Task An_unset_backup_root_never_reaches_the_prompt()
    {
        var (vm, dialog, _) = await CreateViewModelAsync(1, configuredBackupRoot: "");

        Assert.Equal(BackupComparisonKind.BackupFolderNotAvailable, vm.Games[0].ComparisonKind);

        vm.RequestStartupUnsecuredPrompt();

        Assert.Empty(vm.GetUnsecuredRows());
        Assert.Empty(dialog.ConfirmMessages);
        Assert.Equal(0, dialog.PickFolderCalls);
    }
}
