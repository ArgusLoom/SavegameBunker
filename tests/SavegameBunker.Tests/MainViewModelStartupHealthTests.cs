using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// Startup backup-root health: the unreachable/unset/missing-permission reports on load.
/// </summary>
[Collection("MainViewModel")]
public sealed class MainViewModelStartupHealthTests : MainViewModelTestBase
{
    // --- Startup backup-root health --------------------------------------------------

    [Fact]
    public async Task ReportLoad_tells_the_user_when_a_configured_backup_root_is_unreachable()
    {
        var missing = Path.Combine(_dir, "offline-backups");
        var (vm, dialog, _) = CreateViewModel(missing);

        await vm.ReportLoad();

        Assert.NotEmpty(dialog.ErrorMessages);
        Assert.Contains("unreachable", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unreachable", dialog.ErrorMessages[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportLoad_stays_quiet_when_backup_root_is_unset()
    {
        var (vm, dialog, _) = CreateViewModel(backupRoot: null);

        await vm.ReportLoad();

        Assert.Empty(dialog.ErrorMessages);
        Assert.DoesNotContain("unreachable", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportLoad_does_not_add_a_health_dialog_on_the_fatal_path()
    {
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(
            Path.Combine(_dir, "broken.json"),
            new Catalog(),
            [],
            Seeded: false,
            FatalError: "games.json could not be parsed.");
        var vm = new MainViewModel(load, dialog);

        await vm.ReportLoad();

        Assert.Equal("games.json could not be parsed.", vm.StatusMessage);
        Assert.DoesNotContain(
            "unreachable",
            string.Join('\n', dialog.ErrorMessages),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReportLoad_skips_health_when_the_catalog_was_seeded()
    {
        var missing = Path.Combine(_dir, "template-backups");
        var dialog = new RecordingDialogService();
        var load = new CatalogLoad(
            Path.Combine(_dir, "games.json"),
            new Catalog { BackupRoot = missing },
            [],
            Seeded: true,
            FatalError: null);
        var vm = new MainViewModel(load, dialog);

        await vm.ReportLoad();

        Assert.Empty(dialog.ErrorMessages);
        Assert.DoesNotContain("unreachable", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
}
