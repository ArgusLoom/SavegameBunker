using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Direct tests over <see cref="RowActionsCoordinator"/> — the per-row Back up / Restore /
/// Open save folder flows the view model used to hold inline, driven through a recording
/// <see cref="IRowActionHost"/> fake so the exact host-call order is observable. Built over
/// real temp save folders and the real <see cref="BackupService"/>/<see cref="GameActions"/>,
/// matching the established coordinator-test idioms in this suite.
/// </summary>
public sealed class RowActionsCoordinatorTests : IDisposable
{
    private readonly string _dir;

    public RowActionsCoordinatorTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* a leftover temp directory must not fail the test run */ }
    }

    /// <summary>
    /// Records every host call in order as a readable string, returns a configurable backup
    /// root, and can run a hook inside <see cref="IRowActionHost.EnsureBackupRootAsync"/> so a
    /// test can observe the row's state mid-flow.
    /// </summary>
    private sealed class FakeRowActionHost : IRowActionHost
    {
        public List<string> Calls { get; } = new();
        public string? EnsureRootResult { get; set; } = "/tmp/unused-root";
        public bool HasConfiguredRoot { get; set; }
        public string ConfiguredRoot { get; set; } = "/tmp/unused-root";
        public Action? EnsureBackupRootHook { get; set; }

        public Task<string?> EnsureBackupRootAsync()
        {
            Calls.Add("EnsureBackupRoot");
            EnsureBackupRootHook?.Invoke();
            return Task.FromResult(EnsureRootResult);
        }

        public bool TryGetConfiguredBackupRoot(out string root)
        {
            Calls.Add("TryGetConfiguredBackupRoot");
            root = ConfiguredRoot;
            return HasConfiguredRoot;
        }

        public void SetStatus(string message) => Calls.Add("SetStatus:" + message);
        public OperationResult? LastReport { get; private set; }
        public void Report(OperationResult result)
        {
            LastReport = result;
            Calls.Add("Report:" + result.Success);
        }
        public void Rescan(GameRowViewModel row) => Calls.Add("Rescan:" + row.Game.Id);
        public void BackupStarted() => Calls.Add("BackupStarted");
        public void BackupFinished() => Calls.Add("BackupFinished");
        public void RestoreStarted() => Calls.Add("RestoreStarted");
        public void RestoreFinished() => Calls.Add("RestoreFinished");
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

    private GameRowViewModel RowWithTemplate(string id, string displayName, string template) =>
        new(new Game { Id = id, DisplayName = displayName, SaveDirTemplate = template });

    private RowActionsCoordinator Coordinator(BackupService backup, FakeRowActionHost host) =>
        new(backup, new GameActions(backup, new RecordingDialogService()), new RecordingDialogService(), host);

    [Fact]
    public async Task BackupAsync_marks_the_row_busy_before_resolving_the_backup_folder()
    {
        var backup = new BackupService();
        var game = GameWithSaves("game-a", "Game A");
        var row = new GameRowViewModel(game);
        var busyInsideEnsureRoot = default(bool?);

        var host = new FakeRowActionHost
        {
            EnsureRootResult = Path.Combine(_dir, "backups"),
            EnsureBackupRootHook = () => busyInsideEnsureRoot = row.IsBusy,
        };

        await Coordinator(backup, host).BackupAsync(row, new ByteRecorder());

        Assert.True(busyInsideEnsureRoot);
    }

    [Fact]
    public async Task BackupAsync_with_a_cancelled_folder_choice_releases_the_row_and_never_starts_a_backup()
    {
        var backup = new BackupService();
        var row = new GameRowViewModel(GameWithSaves("game-a", "Game A"));

        var host = new FakeRowActionHost { EnsureRootResult = null };

        await Coordinator(backup, host).BackupAsync(row, new ByteRecorder());

        var call = Assert.Single(host.Calls);
        Assert.Equal("EnsureBackupRoot", call);
        Assert.False(row.IsBusy);
    }

    [Fact]
    public async Task BackupAsync_reports_then_rescans_after_a_successful_write_and_balances_the_in_flight_count()
    {
        var backup = new BackupService();
        var row = new GameRowViewModel(GameWithSaves("game-a", "Game A"));
        var backupRoot = Path.Combine(_dir, "backups");

        var host = new FakeRowActionHost { EnsureRootResult = backupRoot };

        await Coordinator(backup, host).BackupAsync(row, new ByteRecorder());

        Assert.Equal(new[]
        {
            "EnsureBackupRoot",
            "BackupStarted",
            "SetStatus:" + Strings.Format("str_backup_inProgress", row.DisplayName),
            "Report:True",
            "Rescan:game-a",
            "BackupFinished",
        }, host.Calls);
        Assert.False(row.IsBusy);
        Assert.Equal(0, row.BackupBytesTotal);
        Assert.Equal(0, row.BackupBytesWritten);
        Assert.True(Directory.GetFiles(backupRoot, "*.zip", SearchOption.AllDirectories).Length > 0);
    }

    [Fact]
    public async Task BackupAsync_reports_a_failed_write_without_rescanning()
    {
        var backup = new BackupService();
        var row = RowWithTemplate("game-a", "Game A", "%UNRESOLVED%/saves");

        var host = new FakeRowActionHost { EnsureRootResult = Path.Combine(_dir, "backups") };

        await Coordinator(backup, host).BackupAsync(row, new ByteRecorder());

        Assert.DoesNotContain(host.Calls, call => call.StartsWith("Rescan:"));
        Assert.Contains("Report:False", host.Calls);
        Assert.Contains("BackupFinished", host.Calls);
        Assert.False(row.IsBusy);
    }

    [Fact]
    public async Task BackupAsync_ignores_a_null_or_already_busy_row()
    {
        var backup = new BackupService();
        var busyRow = new GameRowViewModel(GameWithSaves("game-a", "Game A"));
        busyRow.IsBusy = true;

        var host = new FakeRowActionHost();

        await Coordinator(backup, host).BackupAsync(null, new ByteRecorder());
        await Coordinator(backup, host).BackupAsync(busyRow, new ByteRecorder());

        Assert.Empty(host.Calls);
        // The coordinator ignores a busy row — it does not own it, so it must not release it either.
        Assert.True(busyRow.IsBusy);
    }

    [Fact]
    public async Task RestoreAsync_refuses_without_a_configured_backup_folder_and_releases_the_row()
    {
        var backup = new BackupService();
        var row = new GameRowViewModel(GameWithSaves("game-a", "Game A"));

        var host = new FakeRowActionHost { HasConfiguredRoot = false };

        await Coordinator(backup, host).RestoreAsync(row, new StepRecorder());

        var reportIndex = host.Calls.IndexOf("Report:False");
        Assert.True(reportIndex >= 0);
        Assert.Equal("Report:False", host.Calls[reportIndex]);
        Assert.DoesNotContain("RestoreStarted", host.Calls);
        Assert.False(row.IsBusy);
        Assert.NotNull(host.LastReport);
        Assert.Equal(Strings.Format("str_restore_noBackupFolder", row.DisplayName), host.LastReport!.Message);
        Assert.Single(host.Calls, "TryGetConfiguredBackupRoot");
    }

    [Fact]
    public async Task OpenSaveFolderAsync_reports_the_path_error_sentence_for_an_unresolvable_template()
    {
        var backup = new BackupService();
        var row = RowWithTemplate("game-a", "Game A", "%UNRESOLVED%/saves");

        var host = new FakeRowActionHost();

        await Coordinator(backup, host).OpenSaveFolderAsync(row);

        Assert.Equal(new[] { "Report:False" }, host.Calls);
    }

    [Fact]
    public async Task OpenSaveFolderAsync_reports_not_a_folder_for_a_missing_directory()
    {
        var backup = new BackupService();
        var row = RowWithTemplate("game-a", "Game A", Path.Combine(_dir, "missing-saves").Replace('\\', '/'));

        var host = new FakeRowActionHost();

        await Coordinator(backup, host).OpenSaveFolderAsync(row);

        Assert.Equal(new[] { "Report:False" }, host.Calls);
    }

    private sealed class ByteRecorder : IProgress<long>
    {
        public List<long> Reports { get; } = new();
        public void Report(long value) => Reports.Add(value);
    }

    private sealed class StepRecorder : IProgress<int>
    {
        public List<int> Reports { get; } = new();
        public void Report(int value) => Reports.Add(value);
    }
}
