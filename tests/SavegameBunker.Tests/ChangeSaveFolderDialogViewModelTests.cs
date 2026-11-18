using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// Exercises the Change Save Folder dialog's pre-fill, the not-yet-existing-folder case staying
/// confirmable and the resolve line's two outcomes — the repair route's browse-and-validate path,
/// without a running window.
/// </summary>
public sealed class ChangeSaveFolderDialogViewModelTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly string _dir;

    public ChangeSaveFolderDialogViewModelTests()
    {
        _dir = _temp.DirectoryPath;
    }

    public void Dispose() => _temp.Dispose();

    private static ChangeSaveFolderDialogViewModel CreateViewModel(string currentTemplate) =>
        new("Test Game", currentTemplate, new ThrowingDialogService());

    [Fact]
    public void The_dialog_opens_pre_filled_with_the_rows_current_template()
    {
        var vm = CreateViewModel("%APPDATA%/TestGame");

        Assert.Equal("%APPDATA%/TestGame", vm.SaveFolder);
    }

    [Fact]
    public void The_title_names_the_game()
    {
        var vm = CreateViewModel("%APPDATA%/TestGame");

        Assert.Equal("Change Save Folder — Test Game", vm.Title);
    }

    [Fact]
    public void Confirm_is_enabled_as_soon_as_the_dialog_opens_pre_filled()
    {
        var vm = CreateViewModel("%APPDATA%/TestGame");

        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public void A_blank_path_disables_confirm()
    {
        var vm = CreateViewModel("");

        Assert.False(vm.CanConfirm);
    }

    [Fact]
    public async Task A_folder_that_does_not_exist_yet_reports_it_but_never_blocks_confirm()
    {
        var vm = CreateViewModel(Path.Combine(_dir, "does-not-exist-yet"));

        await WaitForAsync(() => vm.ResolvedStatus == "Folder does not exist yet");

        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public async Task An_existing_folder_reports_file_count_and_size()
    {
        var folder = Path.Combine(_dir, "saves");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "save.dat"), new byte[1024]);

        var vm = CreateViewModel(Path.Combine(_dir, "does-not-exist-yet"));
        vm.SaveFolder = folder;

        await WaitForAsync(() => vm.ResolvedStatus == "1 file, 1.0 KB");

        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public void Resolve_shows_checking_while_in_flight()
    {
        var vm = CreateViewModel(Path.Combine(_dir, "saves"));

        vm.SaveFolder = Path.Combine(_dir, "another-folder");

        Assert.Equal("Checking…", vm.ResolvedStatus);
    }

    [Fact]
    public void Browse_puts_the_picked_folder_in_as_a_template()
    {
        var dialog = new ThrowingDialogService { FolderToReturn = Path.Combine(_dir, "picked") };
        var vm = new ChangeSaveFolderDialogViewModel("Test Game", "%APPDATA%/TestGame", dialog);

        vm.BrowseCommand.Execute(null);

        Assert.Equal(PathResolver.ToTemplate(Path.Combine(_dir, "picked")), vm.SaveFolder);
    }
}
