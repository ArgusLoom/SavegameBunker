using System.IO;
using System.Linq;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Direct tests over <see cref="StatusSweepCoordinator"/> — the status-sweep engine behind the
/// view model's <c>StartStatusScan</c>/<c>RecomputeSpaceAdvisoryAsync</c>. Built
/// over real temp save folders and the real <see cref="BackupService"/>, with <c>onUi</c> an
/// inline counting delegate so every marshalled write is observable without a dispatcher.
/// </summary>
public sealed class StatusSweepCoordinatorTests : IDisposable
{
    private readonly string _dir;

    public StatusSweepCoordinatorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a leftover temp directory must not fail the test run */ }
    }

    private Game GameWithSaves(string id, string displayName)
    {
        var saveDir = Path.Combine(_dir, "saves-" + id);
        Directory.CreateDirectory(saveDir);
        File.WriteAllText(Path.Combine(saveDir, "save.dat"), "payload");

        return new Game
        {
            Id = id,
            DisplayName = displayName,
            SaveDirTemplate = saveDir.Replace('\\', '/'),
        };
    }

    private static GameRowViewModel Row(Game game) => new(game);

    [Fact]
    public void Reset_bumps_each_rows_token_and_sets_both_axes_to_checking()
    {
        var backup = new BackupService();
        var coordinator = new StatusSweepCoordinator(backup, action => action());
        var rows = new[] { Row(GameWithSaves("a", "A")), Row(GameWithSaves("b", "B")) };

        var before = rows.Select(r => r.ScanToken).ToArray();
        var snapshot = coordinator.Reset(rows);

        Assert.Equal(2, snapshot.Count);
        foreach (var (row, token) in snapshot)
        {
            Assert.Equal(row.ScanToken, token);
            Assert.True(token > before[Array.IndexOf(rows, row)]);
            Assert.Equal(SaveStatusKind.Checking, GetKind(row));
            Assert.Equal(BackupComparisonKind.Checking, row.ComparisonKind);
        }
    }

    private static SaveStatusKind GetKind(GameRowViewModel row) =>
        row.StatusText == Strings.Get("str_common_checking") ? SaveStatusKind.Checking : SaveStatusKind.Found;

    [Fact]
    public void Reset_of_no_rows_returns_an_empty_snapshot_and_touches_nothing()
    {
        var backup = new BackupService();
        var coordinator = new StatusSweepCoordinator(backup, action => action());

        var snapshot = coordinator.Reset([]);

        Assert.Empty(snapshot);
    }

    [Fact]
    public async Task WalkAsync_settles_every_row_and_routes_each_write_through_onUi()
    {
        var backup = new BackupService();
        var onUiCalls = 0;
        var coordinator = new StatusSweepCoordinator(backup, action =>
        {
            onUiCalls++;
            action();
        });

        var rows = new[] { Row(GameWithSaves("a", "A")), Row(GameWithSaves("b", "B")) };
        var snapshot = coordinator.Reset(rows);

        var root = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(root);

        await coordinator.WalkAsync(snapshot, hasRoot: true, root: root);

        foreach (var row in rows)
            Assert.NotEqual(BackupComparisonKind.Checking, row.ComparisonKind);

        // Two rows, each writing through onUi twice (the status write, the comparison write).
        Assert.Equal(4, onUiCalls);
    }

    [Fact]
    public async Task WalkAsync_leaves_a_row_alone_when_a_newer_scan_bumped_its_token()
    {
        var backup = new BackupService();
        var coordinator = new StatusSweepCoordinator(backup, action => action());

        var rows = new[] { Row(GameWithSaves("a", "A")), Row(GameWithSaves("b", "B")) };
        var snapshot = coordinator.Reset(rows);

        // A newer scan claims row[0] after this snapshot was taken.
        rows[0].ScanToken++;

        var root = Path.Combine(_dir, "backups");
        Directory.CreateDirectory(root);

        await coordinator.WalkAsync(snapshot, hasRoot: true, root: root);

        Assert.Equal(BackupComparisonKind.Checking, rows[0].ComparisonKind);
        Assert.NotEqual(BackupComparisonKind.Checking, rows[1].ComparisonKind);
    }

    [Fact]
    public async Task WalkAsync_reports_became_reachable_only_on_the_false_to_true_flip()
    {
        var backup = new BackupService();
        var coordinator = new StatusSweepCoordinator(backup, action => action());
        var row = Row(GameWithSaves("a", "A"));
        var root = Path.Combine(_dir, "backups");

        // Missing root: not reachable.
        var snapshot1 = coordinator.Reset([row]);
        var became1 = await coordinator.WalkAsync(snapshot1, hasRoot: true, root: root);
        Assert.False(became1);

        // Root now exists: this is the flip.
        Directory.CreateDirectory(root);
        var snapshot2 = coordinator.Reset([row]);
        var became2 = await coordinator.WalkAsync(snapshot2, hasRoot: true, root: root);
        Assert.True(became2);

        // Still reachable: no flip the second time.
        var snapshot3 = coordinator.Reset([row]);
        var became3 = await coordinator.WalkAsync(snapshot3, hasRoot: true, root: root);
        Assert.False(became3);
    }

    [Fact]
    public async Task ComputeAdvisoryAsync_returns_no_segments_without_a_root_or_any_games()
    {
        var backup = new BackupService();
        var coordinator = new StatusSweepCoordinator(backup, action => action());

        var (free, selected, changed) = await coordinator.ComputeAdvisoryAsync(
            hasRoot: false, root: "", selectedGames: [], changedGames: []);

        Assert.Null(free);
        Assert.Null(selected);
        Assert.Null(changed);
    }

    [Fact]
    public async Task ComputeAdvisoryAsync_names_the_selected_total()
    {
        var backup = new BackupService();
        var coordinator = new StatusSweepCoordinator(backup, action => action());
        var game = GameWithSaves("a", "A");

        var (_, selected, _) = await coordinator.ComputeAdvisoryAsync(
            hasRoot: false, root: "", selectedGames: [game], changedGames: []);

        var expectedSize = backup.ComputeSaveDataSize(game);
        Assert.Equal(
            Strings.Format("str_freeSpace_selectedNeeds", SizeFormat.Describe(expectedSize)),
            selected);
    }
}
