using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Direct unit tests of <see cref="BulkRunRules"/> — no filesystem and no view-model machinery
/// beyond rows built from synthetic <see cref="BackupComparison"/> values, matching
/// <see cref="GameRowViewModelTests"/>'s no-filesystem row test shape. Expected sentences are
/// always composed with <see cref="Strings.Format"/> so the assertions follow the authored
/// wording instead of duplicating it.
/// </summary>
public sealed class BulkRunRulesTests
{
    private static GameRowViewModel MakeRow(string id, string name, BackupComparisonKind kind)
    {
        var row = new GameRowViewModel(new Game
        {
            Id = id,
            DisplayName = name,
            SaveDirTemplate = "%APPDATA%/x",
        });

        row.ApplyBackupComparison(kind switch
        {
            BackupComparisonKind.Checking => BackupComparison.Checking(),
            BackupComparisonKind.UpToDate => BackupComparison.UpToDate(new DateTime(2026, 1, 1, 12, 0, 0)),
            BackupComparisonKind.SavesChangedSinceBackup => BackupComparison.SavesChangedSinceBackup(
                new DateTime(2026, 1, 1, 12, 0, 0), TimeSpan.FromHours(2)),
            BackupComparisonKind.NeverBackedUp => BackupComparison.NeverBackedUp(),
            BackupComparisonKind.NoSaveData => BackupComparison.NoSaveData(null),
            _ => BackupComparison.FolderNotAvailable(),
        });

        return row;
    }

    [Theory]
    [InlineData(BackupComparisonKind.Checking, false)]
    [InlineData(BackupComparisonKind.UpToDate, false)]
    [InlineData(BackupComparisonKind.SavesChangedSinceBackup, true)]
    [InlineData(BackupComparisonKind.NeverBackedUp, true)]
    [InlineData(BackupComparisonKind.NoSaveData, false)]
    [InlineData(BackupComparisonKind.BackupFolderNotAvailable, false)]
    public void NeedsBackup_is_true_only_for_changed_and_never_backed_up(
        BackupComparisonKind kind, bool expected) =>
        Assert.Equal(expected, BulkRunRules.NeedsBackup(kind));

    [Fact]
    public void ChangedTargets_keeps_input_order_and_skips_busy_rows()
    {
        var a = MakeRow("a", "Alpha", BackupComparisonKind.SavesChangedSinceBackup);
        var b = MakeRow("b", "Beta", BackupComparisonKind.NeverBackedUp);
        var c = MakeRow("c", "Gamma", BackupComparisonKind.UpToDate);
        var d = MakeRow("d", "Delta", BackupComparisonKind.SavesChangedSinceBackup);
        d.IsBusy = true;

        var targets = BulkRunRules.ChangedTargets([a, b, c, d]);

        Assert.Equal(new[] { a, b }, targets);
    }

    [Fact]
    public void SelectedTargets_keeps_input_order_and_skips_busy_rows()
    {
        var a = MakeRow("a", "Alpha", BackupComparisonKind.UpToDate);
        var b = MakeRow("b", "Beta", BackupComparisonKind.NeverBackedUp);
        var c = MakeRow("c", "Gamma", BackupComparisonKind.NoSaveData);
        a.IsSelected = true;
        c.IsSelected = true;
        c.IsBusy = true;

        var targets = BulkRunRules.SelectedTargets([a, b, c]);

        Assert.Equal(new[] { a }, targets);
    }

    [Fact]
    public void BackupOutcome_names_the_single_game_when_one_target_succeeds()
    {
        var targets = new[] { MakeRow("a", "Alpha", BackupComparisonKind.NeverBackedUp) };

        var result = BulkRunRules.BackupOutcome(
            targets, [], "str_bulkBackup_allSucceeded", "str_bulkBackup_partialFailure");

        Assert.True(result.Success);
        Assert.Equal(Strings.Format("str_bulkBackup_one", "Alpha"), result.Message);
    }

    [Fact]
    public void BackupOutcome_uses_the_all_succeeded_key_for_several_targets()
    {
        var targets = new[]
        {
            MakeRow("a", "Alpha", BackupComparisonKind.NeverBackedUp),
            MakeRow("b", "Beta", BackupComparisonKind.SavesChangedSinceBackup),
        };

        var result = BulkRunRules.BackupOutcome(
            targets, [], "str_bulkBackup_allSucceeded", "str_bulkBackup_partialFailure");

        Assert.True(result.Success);
        Assert.Equal(Strings.Format("str_bulkBackup_allSucceeded", 2), result.Message);
    }

    [Fact]
    public void BackupOutcome_reports_partial_failure_with_counts_and_names()
    {
        var targets = new[]
        {
            MakeRow("a", "Alpha", BackupComparisonKind.NeverBackedUp),
            MakeRow("b", "Beta", BackupComparisonKind.SavesChangedSinceBackup),
            MakeRow("c", "Gamma", BackupComparisonKind.NeverBackedUp),
        };

        var result = BulkRunRules.BackupOutcome(
            targets, ["Beta", "Gamma"], "str_bulkBackup_allSucceeded", "str_bulkBackup_partialFailure");

        Assert.False(result.Success);
        Assert.Equal(Strings.Format("str_bulkBackup_partialFailure", 1, 3, "Beta, Gamma"), result.Message);
    }

    [Fact]
    public void RestoreOutcome_is_a_cancel_when_nothing_was_restored_and_nothing_failed()
    {
        var targets = new[] { MakeRow("a", "Alpha", BackupComparisonKind.UpToDate) };

        var result = BulkRunRules.RestoreOutcome(targets, 0, [], []);

        Assert.False(result.Success);
        Assert.True(result.Cancelled);
        Assert.Equal(Strings.Get("str_restore_cancelled"), result.Message);
    }

    [Fact]
    public void RestoreOutcome_failure_wins_over_declined()
    {
        var targets = new[]
        {
            MakeRow("a", "Alpha", BackupComparisonKind.UpToDate),
            MakeRow("b", "Beta", BackupComparisonKind.UpToDate),
            MakeRow("c", "Gamma", BackupComparisonKind.UpToDate),
        };

        var result = BulkRunRules.RestoreOutcome(targets, 1, ["Gamma"], ["Beta"]);

        Assert.False(result.Success);
        Assert.False(result.Cancelled);
        Assert.Equal(Strings.Format("str_bulkRestoreSelected_partialFailure", 1, 3, "Gamma"), result.Message);
    }

    [Fact]
    public void RestoreOutcome_names_declined_games_without_claiming_all()
    {
        var targets = new[]
        {
            MakeRow("a", "Alpha", BackupComparisonKind.UpToDate),
            MakeRow("b", "Beta", BackupComparisonKind.UpToDate),
        };

        var result = BulkRunRules.RestoreOutcome(targets, 1, [], ["Beta"]);

        Assert.True(result.Success);
        Assert.False(result.Cancelled);
        Assert.Equal(Strings.Format("str_bulkRestoreSelected_partialSkipped", 1, 2, "Beta"), result.Message);
    }

    [Fact]
    public void RestoreOutcome_names_the_single_game_or_counts_all()
    {
        var single = new[] { MakeRow("a", "Alpha", BackupComparisonKind.UpToDate) };
        var one = BulkRunRules.RestoreOutcome(single, 1, [], []);

        var pair = new[]
        {
            MakeRow("a", "Alpha", BackupComparisonKind.UpToDate),
            MakeRow("b", "Beta", BackupComparisonKind.UpToDate),
        };
        var all = BulkRunRules.RestoreOutcome(pair, 2, [], []);

        Assert.True(one.Success);
        Assert.Equal(Strings.Format("str_bulkRestore_one", "Alpha"), one.Message);
        Assert.True(all.Success);
        Assert.Equal(Strings.Format("str_bulkRestoreSelected_allSucceeded", 2), all.Message);
    }
}
