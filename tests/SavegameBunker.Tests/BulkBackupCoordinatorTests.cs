using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Direct tests over <see cref="BulkBackupCoordinator"/> — the claim/release order behind the
/// view model's bulk commands, tested without the view model around it. Built over real temp save folders and the real
/// <see cref="BackupService"/>/<see cref="GameActions"/>, with a synchronous recording
/// <see cref="IProgress{T}"/> so the report order is observable (a real <see cref="Progress{T}"/>
/// would post through a synchronization context the plain xUnit host does not have).
/// </summary>
public sealed class BulkBackupCoordinatorTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly string _dir;

    public BulkBackupCoordinatorTests()
    {
        _dir = _temp.DirectoryPath;
    }

    public void Dispose() => _temp.Dispose();

    private sealed class ByteProgressRecorder : IProgress<long>
    {
        public List<long> Reports { get; } = [];

        public void Report(long value) => Reports.Add(value);
    }

    private List<GameRowViewModel> Rows(params Game[] games) =>
        [.. games.Select(game => new GameRowViewModel(game))];

    private static Dictionary<GameRowViewModel, IProgress<long>> ByteProgressFor(
        IEnumerable<GameRowViewModel> rows)
    {
        var map = new Dictionary<GameRowViewModel, IProgress<long>>();
        foreach (var row in rows)
            map[row] = new ByteProgressRecorder();
        return map;
    }

    [Fact]
    public async Task BackupAsync_resets_targets_to_checking_and_reports_the_start_before_claiming_any_row()
    {
        var backup = new BackupService();
        var rows = Rows(TestGames.WithNamedSaves(_dir, "game-a", "Game A"), TestGames.WithNamedSaves(_dir, "game-b", "Game B"));
        var seenAtStart = default(List<(BackupComparisonKind Kind, bool Busy)>);

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), _ => { });

        var run = await coordinator.BackupAsync(
            rows, Path.Combine(_dir, "backups"), (_, _) => { }, ByteProgressFor(rows),
            (total, first) =>
            {
                Assert.Equal(2, total);
                Assert.Equal("Game A", first);
                seenAtStart = [.. rows.Select(row => (row.ComparisonKind, row.IsBusy))];
            },
            () => { });

        Assert.NotNull(run);
        Assert.NotNull(seenAtStart);
        // Inside onStarted every target is already reset to Checking and none is busy yet —
        // the claim (IsBusy) comes strictly after the start callback.
        Assert.All(seenAtStart!, entry =>
        {
            Assert.Equal(BackupComparisonKind.Checking, entry.Kind);
            Assert.False(entry.Busy);
        });
    }

    [Fact]
    public async Task BackupAsync_releases_every_target_and_calls_onFinished_once_after_the_loop()
    {
        var backup = new BackupService();
        var rows = Rows(TestGames.WithNamedSaves(_dir, "game-a", "Game A"), TestGames.WithNamedSaves(_dir, "game-b", "Game B"));
        var releasedAtFinish = default(List<bool>);
        var finishCount = 0;

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), _ => { });

        var run = await coordinator.BackupAsync(
            rows, Path.Combine(_dir, "backups"), (_, _) => { }, ByteProgressFor(rows),
            (_, _) => { },
            () =>
            {
                finishCount++;
                releasedAtFinish = [.. rows.Select(row => row.IsBusy)];
            });

        Assert.NotNull(run);
        Assert.Equal(1, finishCount);
        // Inside onFinished every target is already released.
        Assert.All(releasedAtFinish!, busy => Assert.False(busy));
        // And they stay released after the call returns.
        Assert.All(rows, row => Assert.False(row.IsBusy));
    }

    [Fact]
    public async Task BackupAsync_still_releases_and_calls_onFinished_when_the_loop_throws()
    {
        var backup = new BackupService();
        var rows = Rows(TestGames.WithNamedSaves(_dir, "game-a", "Game A"), TestGames.WithNamedSaves(_dir, "game-b", "Game B"));
        var finishCount = 0;

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), _ => { });

        // A byteProgress dictionary missing the second target makes the loop throw as soon as it
        // reaches that row — the exact shape of an unexpected mid-loop failure.
        var byteProgress = ByteProgressFor([rows[0]]);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() => coordinator.BackupAsync(
            rows, Path.Combine(_dir, "backups"), (_, _) => { }, byteProgress,
            (_, _) => { },
            () => finishCount++));

        Assert.Equal(1, finishCount);
        Assert.All(rows, row => Assert.False(row.IsBusy));
        Assert.All(rows, row => Assert.Equal(0, row.BackupBytesTotal));
        Assert.All(rows, row => Assert.Equal(0, row.BackupBytesWritten));
    }

    [Fact]
    public async Task BackupAsync_with_no_targets_returns_null_and_touches_nothing()
    {
        var backup = new BackupService();
        var rows = Rows(TestGames.WithNamedSaves(_dir, "game-a", "Game A"));
        var started = 0;
        var finished = 0;

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), _ => { });

        var run = await coordinator.BackupAsync(
            new List<GameRowViewModel>(), Path.Combine(_dir, "backups"), (_, _) => { },
            ByteProgressFor(rows),
            (_, _) => started++,
            () => finished++);

        Assert.Null(run);
        Assert.Equal(0, started);
        Assert.Equal(0, finished);
        Assert.False(rows[0].IsBusy);
    }

    [Fact]
    public async Task BackupAsync_collects_a_failed_game_and_continues_with_the_rest()
    {
        var backup = new BackupService();
        var backupRoot = Path.Combine(_dir, "backups");

        // First target's template is unresolvable, so its backup fails while the run continues.
        var broken = new Game
        {
            Id = "game-broken",
            DisplayName = "Broken Game",
            SaveDirTemplate = "%UNRESOLVED%/saves",
        };
        var good = TestGames.WithNamedSaves(_dir, "game-good", "Good Game");
        var rows = Rows(broken, good);

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), action => action());

        var reported = new List<(int Index, string Name)>();
        var run = await coordinator.BackupAsync(
            rows, backupRoot, (index, name) => reported.Add((index, name)), ByteProgressFor(rows),
            (_, _) => { }, () => { });

        Assert.NotNull(run);
        Assert.Equal(["Broken Game"], run.Failed);
        Assert.Single(run.FailureDetails);

        // The second game's archive exists and the recorded progress covered both rows, in order.
        var gameDir = Path.Combine(backupRoot, good.Id);
        Assert.Single(Directory.GetFiles(gameDir, "*.zip"));
        Assert.Equal(
            new[] { (1, "Broken Game"), (2, "Good Game") },
            reported.ToArray());
    }

    [Fact]
    public async Task BackupAsync_collects_the_message_of_a_game_whose_backup_succeeded_with_a_warning()
    {
        var backup = new BackupService();
        var backupRoot = Path.Combine(_dir, "backups");
        var locked = TestGames.WithNamedSaves(_dir, "game-locked", "Locked Game");
        var clean = TestGames.WithNamedSaves(_dir, "game-clean", "Clean Game");
        File.WriteAllText(Path.Combine(_dir, "saves-game-locked", "other.dat"), "readable");
        var rows = Rows(locked, clean);

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), action => action());

        BulkBackupRun? run;
        using (new FileStream(Path.Combine(_dir, "saves-game-locked", "save.dat"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            run = await coordinator.BackupAsync(
                rows, backupRoot, (_, _) => { }, ByteProgressFor(rows), (_, _) => { }, () => { });
        }

        // Both archives were written, so nothing failed — but the locked game's archive is
        // incomplete, and its message is the only place that says so.
        Assert.NotNull(run);
        Assert.Empty(run.Failed);
        var warning = Assert.Single(run.Warnings);
        Assert.Contains("Locked Game", warning, StringComparison.Ordinal);
        Assert.Contains("NOT in this backup", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackupAsync_routes_every_loop_row_write_through_onUi()
    {
        var backup = new BackupService();
        var rows = Rows(TestGames.WithNamedSaves(_dir, "game-a", "Game A"), TestGames.WithNamedSaves(_dir, "game-b", "Game B"));
        var onUiCalls = 0;

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), action =>
            {
                onUiCalls++;
                action();
            });

        var run = await coordinator.BackupAsync(
            rows, Path.Combine(_dir, "backups"), (_, _) => { }, ByteProgressFor(rows),
            (_, _) => { }, () => { });

        Assert.NotNull(run);
        // Four routed calls per successful row: the progress report, the byte-total set, the
        // RestoredArchiveTakenAt clear and the closing counter clear — 4 × 2 targets = 8.
        Assert.Equal(8, onUiCalls);
    }

    [Fact]
    public async Task BackupAsync_reports_each_game_before_writing_its_archive()
    {
        var backup = new BackupService();
        var backupRoot = Path.Combine(_dir, "backups");
        var gameA = TestGames.WithNamedSaves(_dir, "game-a", "Game A");
        var gameB = TestGames.WithNamedSaves(_dir, "game-b", "Game B");
        var rows = Rows(gameA, gameB);

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), action => action());

        var reported = new List<(int Index, string Name, int ArchiveCountSoFar)>();

        var run = await coordinator.BackupAsync(
            rows, backupRoot,
            (index, name) =>
            {
                var gameId = index == 1 ? gameA.Id : gameB.Id;
                var gameDir = Path.Combine(backupRoot, gameId);
                var archiveCount = Directory.Exists(gameDir) ? Directory.GetFiles(gameDir, "*.zip").Length : 0;
                reported.Add((index, name, archiveCount));
            },
            ByteProgressFor(rows), (_, _) => { }, () => { });

        Assert.NotNull(run);
        Assert.Equal(
            new[] { (1, "Game A", 0), (2, "Game B", 0) },
            reported);
    }

    private static async Task WriteOrdinaryBackupAsync(BackupService backup, Game game, string backupRoot)
    {
        // A BackupService.Backup write through GameActions' same seam — the archive the restore
        // loop's TryPlanNewestOrdinaryAsync will pick up.
        var result = await Task.Run(() => backup.Backup(game, backupRoot));
        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task RestoreAsync_reports_each_item_in_order_and_releases_every_target()
    {
        var backup = new BackupService();
        var backupRoot = Path.Combine(_dir, "backups");

        var first = TestGames.WithNamedSaves(_dir, "game-first", "First Game");
        var second = TestGames.WithNamedSaves(_dir, "game-second", "Second Game");
        await WriteOrdinaryBackupAsync(backup, first, backupRoot);
        await WriteOrdinaryBackupAsync(backup, second, backupRoot);

        // Recreate the save folders the restore will replace (the backups consumed their
        // content walk only), so the restore's safety backup has something to consider.
        Directory.CreateDirectory(Path.Combine(_dir, "saves-game-first"));
        File.WriteAllText(Path.Combine(_dir, "saves-game-first", "save.dat"), "restored-content-first");
        Directory.CreateDirectory(Path.Combine(_dir, "saves-game-second"));
        File.WriteAllText(Path.Combine(_dir, "saves-game-second", "save.dat"), "restored-content-second");

        var dialog = new RecordingDialogService { ConfirmReturnSequence = new Queue<bool>([true, false]) };
        var rows = Rows(first, second);

        var coordinator = new BulkBackupCoordinator(backup, new GameActions(backup, dialog), _ => { });

        var itemCalls = new List<(int Index, string Name)>();
        var finishCount = 0;
        var releasedAtFinish = default(List<bool>);

        var run = await coordinator.RestoreAsync(
            rows, backupRoot, StepProgressFor(rows),
            (total, first_) => { Assert.Equal(2, total); Assert.Equal("First Game", first_); },
            (index, name) => itemCalls.Add((index, name)),
            () =>
            {
                finishCount++;
                releasedAtFinish = [.. rows.Select(row => row.IsBusy)];
            });

        Assert.NotNull(run);
        Assert.Equal(
            new[] { (1, "First Game"), (2, "Second Game") },
            itemCalls);
        Assert.Equal(1, run.Restored);
        Assert.Empty(run.Failed);
        Assert.Equal(["Second Game"], run.Declined);
        // Two confirms were asked, one per game, in list order.
        Assert.Equal(2, dialog.ConfirmMessages.Count);
        Assert.Equal(1, finishCount);
        Assert.All(releasedAtFinish!, busy => Assert.False(busy));
        Assert.All(rows, row => Assert.False(row.IsBusy));
    }

    [Fact]
    public async Task RestoreAsync_with_no_targets_returns_null_and_touches_nothing()
    {
        var backup = new BackupService();
        var rows = Rows(TestGames.WithNamedSaves(_dir, "game-a", "Game A"));
        var started = 0;
        var items = 0;
        var finished = 0;

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService()), _ => { });

        var run = await coordinator.RestoreAsync(
            new List<GameRowViewModel>(), Path.Combine(_dir, "backups"), StepProgressFor(rows),
            (_, _) => started++, (_, _) => items++, () => finished++);

        Assert.Null(run);
        Assert.Equal(0, started);
        Assert.Equal(0, items);
        Assert.Equal(0, finished);
        Assert.False(rows[0].IsBusy);
    }

    [Fact]
    public async Task RestoreAsync_counts_a_game_without_an_ordinary_backup_as_failed_and_continues()
    {
        var backup = new BackupService();
        var backupRoot = Path.Combine(_dir, "backups");

        // First game has no archive under the backup root at all; the second one does.
        var missing = TestGames.WithNamedSaves(_dir, "game-missing", "Missing Game");
        var present = TestGames.WithNamedSaves(_dir, "game-present", "Present Game");
        await WriteOrdinaryBackupAsync(backup, present, backupRoot);

        var dialog = new RecordingDialogService { ConfirmToReturn = true };
        var rows = Rows(missing, present);

        var coordinator = new BulkBackupCoordinator(backup, new GameActions(backup, dialog), _ => { });

        var run = await coordinator.RestoreAsync(
            rows, backupRoot, StepProgressFor(rows), (_, _) => { }, (_, _) => { }, () => { });

        Assert.NotNull(run);
        Assert.Equal(["Missing Game"], run.Failed);
        Assert.Single(run.FailureDetails);
        Assert.Equal(1, run.Restored);
        // The second game still reached its confirmation despite the first game's failure.
        Assert.Single(dialog.ConfirmMessages);
        Assert.All(rows, row => Assert.False(row.IsBusy));
    }

    [Fact]
    public async Task RestoreAsync_still_clears_step_display_and_calls_onFinished_when_the_loop_throws()
    {
        var backup = new BackupService();
        var backupRoot = Path.Combine(_dir, "backups");
        var first = TestGames.WithNamedSaves(_dir, "game-first", "First Game");
        var second = TestGames.WithNamedSaves(_dir, "game-second", "Second Game");
        await WriteOrdinaryBackupAsync(backup, first, backupRoot);
        await WriteOrdinaryBackupAsync(backup, second, backupRoot);
        Directory.CreateDirectory(Path.Combine(_dir, "saves-game-first"));
        File.WriteAllText(Path.Combine(_dir, "saves-game-first", "save.dat"), "content-first");

        var rows = Rows(first, second);
        // The second row is mid-restore (a step shown) when the loop throws on it: a step reporter
        // missing from the map makes the loop throw once it reaches that row, the same shape of
        // unexpected mid-loop failure the backup test above uses.
        rows[1].RestoreStep = 2;
        rows[1].RestoreStepText = "Extracting";
        var stepProgress = StepProgressFor([rows[0]]);
        var finishCount = 0;

        var coordinator = new BulkBackupCoordinator(
            backup, new GameActions(backup, new RecordingDialogService { ConfirmToReturn = true }), _ => { });

        await Assert.ThrowsAsync<KeyNotFoundException>(() => coordinator.RestoreAsync(
            rows, backupRoot, stepProgress, (_, _) => { }, (_, _) => { }, () => finishCount++));

        Assert.Equal(1, finishCount);
        Assert.All(rows, row => Assert.False(row.IsBusy));
        Assert.All(rows, row => Assert.Equal(0, row.RestoreStep));
        Assert.All(rows, row => Assert.Equal("", row.RestoreStepText));
    }

    private static Dictionary<GameRowViewModel, IProgress<int>> StepProgressFor(
        IEnumerable<GameRowViewModel> rows)
    {
        var map = new Dictionary<GameRowViewModel, IProgress<int>>();
        foreach (var row in rows)
            map[row] = new StepProgressRecorder();
        return map;
    }

    /// <summary>A synchronous recording stand-in for a per-row restore-step reporter.</summary>
    private sealed class StepProgressRecorder : IProgress<int>
    {
        public List<int> Reports { get; } = [];

        public void Report(int value) => Reports.Add(value);
    }
}
