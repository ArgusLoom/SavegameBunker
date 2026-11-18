using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// The shared fixture and builders of the MainViewModel test classes, split from one
/// 3,000-line file along its section markers (ITEM-23). Abstract and therefore not sealed —
/// the one reason CLAUDE.md allows it.
/// </summary>
public abstract class MainViewModelTestBase : IDisposable
{
    private readonly TempDir _temp = new();
    protected readonly string _dir;
    protected readonly string _suggestedStartFolder;

    protected MainViewModelTestBase()
    {
        _dir = _temp.DirectoryPath;
        _suggestedStartFolder = Path.Combine(_dir, "suggested-start");
    }

    public void Dispose() => _temp.Dispose();

    protected (MainViewModel Vm, RecordingDialogService Dialog, string CatalogPath) CreateViewModel(string? backupRoot)
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);

        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog
        {
            BackupRoot = backupRoot,
            Games =
            [
                new() { Id = "test-game", DisplayName = "Test Game", SaveDirTemplate = saveDir.Replace('\\', '/') },
            ],
        };

        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(catalogPath, catalog, [], false, null);
        var vm = new MainViewModel(load, dialog, () => _suggestedStartFolder);
        return (vm, dialog, catalogPath);
    }

    /// <summary>
    /// Same construction shape as <see cref="CreateViewModel"/>, extended to <paramref name="count"/>
    /// games, for tests that need a genuine partial selection to exercise
    /// <see cref="MainViewModel.HeaderCheckState"/>'s Indeterminate state. Deliberately synchronous
    /// (no <c>await</c> anywhere in this method or between calling it and reading
    /// <see cref="MainViewModel.GamesView"/>/<see cref="MainViewModel.HeaderCheckState"/>): that
    /// live <see cref="System.ComponentModel.ICollectionView"/> is a WPF <c>DispatcherObject</c>
    /// bound to whichever thread constructs it, and a plain xUnit host has no
    /// <see cref="System.Threading.SynchronizationContext"/> to keep a test's continuation on that
    /// same thread across a real <c>await</c> yield (e.g. <c>WaitForAsync</c>'s <c>Task.Delay</c>) —
    /// reading it from a different thread throws. Callers of this helper must not <c>await</c>
    /// anything between calling it and reading either property.
    /// </summary>
    protected (MainViewModel Vm, RecordingDialogService Dialog) CreateViewModelWithGames(int count)
    {
        var games = new List<Game>();
        for (var i = 0; i < count; i++)
        {
            var saveDir = Path.Combine(_dir, $"hdr-saves-{i}");
            Directory.CreateDirectory(saveDir);
            File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
            games.Add(new Game { Id = $"hdr-game-{i}", DisplayName = $"Header Game {i}", SaveDirTemplate = saveDir.Replace('\\', '/') });
        }

        var catalogPath = Path.Combine(_dir, "hdr-games.json");
        var catalog = new Catalog { BackupRoot = Path.Combine(_dir, "backups"), Games = games };
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(catalogPath, catalog, [], false, null);
        var vm = new MainViewModel(load, dialog);
        return (vm, dialog);
    }

    /// <summary>
    /// Same shape as <see cref="CreateViewModelWithGames(int)"/>, but with caller-chosen display
    /// names — the search-filter tests need distinct, searchable names rather than the
    /// generic "Header Game N" the other helper produces. Deliberately synchronous for the same
    /// GamesView dispatcher-affinity reason documented there.
    /// </summary>
    protected (MainViewModel Vm, RecordingDialogService Dialog) CreateNamedViewModel(params string[] displayNames)
    {
        var games = new List<Game>();
        for (var i = 0; i < displayNames.Length; i++)
        {
            var saveDir = Path.Combine(_dir, $"search-saves-{i}");
            Directory.CreateDirectory(saveDir);
            File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);
            games.Add(new Game { Id = $"search-game-{i}", DisplayName = displayNames[i], SaveDirTemplate = saveDir.Replace('\\', '/') });
        }

        var catalogPath = Path.Combine(_dir, "search-games.json");
        var catalog = new Catalog { BackupRoot = Path.Combine(_dir, "backups"), Games = games };
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(catalogPath, catalog, [], false, null);
        var vm = new MainViewModel(load, dialog);
        return (vm, dialog);
    }

    /// <summary>
    /// Same starting catalog as <see cref="CreateViewModel"/>, but written to disk first so the
    /// Find games write-path facts below can assert byte-for-byte equality of the catalog file,
    /// not just an entry count on the in-memory <see cref="CatalogLoad"/>.
    /// </summary>
    protected async Task<(MainViewModel Vm, RecordingDialogService Dialog, string CatalogPath)> CreateViewModelWithSavedCatalogAsync(string? backupRoot = null)
    {
        var saveDir = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(saveDir);
        File.WriteAllBytes(Path.Combine(saveDir, "save.dat"), [1, 2, 3]);

        var catalogPath = Path.Combine(_dir, "games.json");
        var catalog = new Catalog
        {
            BackupRoot = backupRoot,
            Games =
            [
                new() { Id = "test-game", DisplayName = "Test Game", SaveDirTemplate = saveDir.Replace('\\', '/') },
            ],
        };
        await GameCatalog.SaveAsync(catalogPath, catalog);

        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);
        return (vm, dialog, catalogPath);
    }

    protected static string SaveFilePath(string dir) => Path.Combine(dir, "save.dat");

    /// <summary>
    /// Writes a real archive for the row's already-existing save data via a standalone
    /// <see cref="BackupService"/> instance — the same write path <c>MainViewModel</c>'s own
    /// <c>_backup</c> field uses, so the fixture's backups are indistinguishable from ones a real
    /// backup gesture would have produced. The archive's file-name timestamp truncates to whole
    /// seconds while <c>File.LastWriteTime</c> keeps sub-second precision, so a backup taken in the
    /// same wall-clock second as the save file's write can otherwise parse as earlier than it —
    /// pushing the save file's own timestamp two seconds back removes that race and gives every
    /// caller a deterministic "up to date" starting point to build a scenario from.
    /// </summary>
    protected BackupArchive SeedBackup(string backupRoot, Game game)
    {
        var result = new BackupService().Backup(game, backupRoot);
        Assert.True(result.Success, result.Message);
        var archive = new BackupService().ListBackups(backupRoot, game.Id)[0];
        File.SetLastWriteTime(SaveFilePath(Path.Combine(_dir, "saves")), archive.TakenAt.AddSeconds(-2));
        return archive;
    }

    /// <summary>
    /// Builds a view model over <paramref name="gameCount"/> games, each with a real, non-empty
    /// save directory so a real <c>BackupService.Backup</c> call against it can succeed, then waits
    /// for the construction-time sweep to settle before returning — every bulk-backup test forces
    /// its own comparison kinds afterward via <see cref="GameRowViewModel.ApplyBackupComparison"/>,
    /// the same synthetic-state technique <c>GameRowViewModelTests</c> uses.
    /// </summary>
    protected async Task<(MainViewModel Vm, RecordingDialogService Dialog, string BackupRoot)> CreateMultiGameViewModelAsync(int gameCount)
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
        var catalog = new Catalog { BackupRoot = backupRoot, Games = games };
        await GameCatalog.SaveAsync(catalogPath, catalog);

        var dialog = new RecordingDialogService();
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, dialog);

        await WaitForAsync(() => vm.Games.All(row => row.ComparisonKind != BackupComparisonKind.Checking));

        return (vm, dialog, backupRoot);
    }
}
