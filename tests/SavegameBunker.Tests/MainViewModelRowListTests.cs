using System.IO;
using System.IO.Compression;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;
using static SavegameBunker.Tests.TestWait;

namespace SavegameBunker.Tests;

/// <summary>
/// Sorted insert on screen and dropping a folder into Add Game.
/// </summary>
[Collection("MainViewModel")]
public sealed class MainViewModelRowListTests : MainViewModelTestBase
{
    // --- Sorted insert on screen -------------------------------------------------------

    [Fact]
    public async Task Adding_a_game_whose_name_sorts_first_puts_its_row_at_index_zero()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("aaa", "AAA", Path.Combine(_dir, "aaa").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        Assert.Equal("aaa", vm.Games[0].Game.Id);
        Assert.Equal("test-game", vm.Games[1].Game.Id);
    }

    [Fact]
    public async Task Adding_a_game_whose_name_sorts_last_puts_its_row_at_the_end()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("zzz", "ZZZ", Path.Combine(_dir, "zzz").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        Assert.Equal("test-game", vm.Games[0].Game.Id);
        Assert.Equal("zzz", vm.Games[1].Game.Id);
    }

    [Fact]
    public async Task Adding_a_game_whose_name_ties_puts_the_new_row_after_the_existing_one()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("test-game-2", "Test Game", Path.Combine(_dir, "tie").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        Assert.Equal("test-game", vm.Games[0].Game.Id);
        Assert.Equal("test-game-2", vm.Games[1].Game.Id);
    }

    [Fact]
    public async Task Adding_a_game_whose_name_ties_only_by_letter_case_puts_the_new_row_after_it()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("test-game-2", "test game", Path.Combine(_dir, "case-tie").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        Assert.Equal("test-game", vm.Games[0].Game.Id);
        Assert.Equal("test-game-2", vm.Games[1].Game.Id);
    }

    [Fact]
    public async Task A_scan_that_adds_several_games_places_each_at_its_own_sorted_position()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.FindGamesSelectionToReturn = new List<KnownGame>
        {
            new() { Id = "zebra", DisplayName = "Zebra", SaveDirTemplate = "%APPDATA%/Zebra" },
            new() { Id = "alpha", DisplayName = "Alpha", SaveDirTemplate = "%APPDATA%/Alpha" },
        };

        vm.FindGamesCommand.Execute(null);
        await WaitForAsync(() => vm.FindGamesCommand.CanExecute(null) && vm.Games.Count == 3);

        Assert.Equal(["alpha", "test-game", "zebra"], vm.Games.Select(row => row.Game.Id));
    }

    [Fact]
    public async Task A_checked_row_stays_checked_after_an_add_repositions_it()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        vm.Games[0].IsSelected = true;
        dialog.AddGameToReturn = new AddGameResult("aaa", "AAA", Path.Combine(_dir, "aaa-sel").Replace('\\', '/'));

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        var original = Assert.Single(vm.Games, row => row.Game.Id == "test-game");
        Assert.True(original.IsSelected);
        Assert.False(vm.Games.Single(row => row.Game.Id == "aaa").IsSelected);
    }

    [Fact]
    public async Task Change_save_folder_repairs_the_row_it_was_opened_from_when_on_screen_order_differs_from_the_catalog()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        dialog.AddGameToReturn = new AddGameResult("aaa", "AAA", Path.Combine(_dir, "aaa-csf").Replace('\\', '/'));
        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => vm.Games.Count == 2);

        var original = Assert.Single(vm.Games, row => row.Game.Id == "test-game");
        var neighbour = Assert.Single(vm.Games, row => row.Game.Id == "aaa");
        var originalTemplate = original.Game.SaveDirTemplate;
        var neighbourTemplate = neighbour.Game.SaveDirTemplate;
        var newFolder = Path.Combine(_dir, "moved-after-sort");
        Directory.CreateDirectory(newFolder);
        dialog.ChangeSaveFolderToReturn = newFolder;

        vm.ChangeSaveFolderCommand.Execute(original);
        await WaitForAsync(() => vm.ChangeSaveFolderCommand.CanExecute(original));

        var repaired = Assert.Single(vm.Games, row => row.Game.Id == "test-game");
        Assert.Equal(newFolder, repaired.Game.SaveDirTemplate);
        Assert.Equal(neighbourTemplate, Assert.Single(vm.Games, row => row.Game.Id == "aaa").Game.SaveDirTemplate);
        Assert.NotEqual(originalTemplate, repaired.Game.SaveDirTemplate);
    }

    [Fact]
    public async Task Refresh_over_an_unsorted_file_still_shows_the_rows_in_file_order()
    {
        var catalogPath = Path.Combine(_dir, "unsorted-games.json");
        File.WriteAllText(catalogPath, """
            {
              "backupRoot": "E:/SavegameBunkerBackups",
              "games": [
                { "id": "b", "displayName": "B", "saveDirTemplate": "%APPDATA%/B" },
                { "id": "a", "displayName": "A", "saveDirTemplate": "%APPDATA%/A" }
              ]
            }
            """);
        var load = await GameCatalog.LoadAsync(catalogPath);
        var vm = new MainViewModel(load, new RecordingDialogService());

        Assert.Equal(["b", "a"], vm.Games.Select(row => row.Game.Id));

        await WaitForAsync(() => vm.RefreshCommand.CanExecute(null));
        vm.RefreshCommand.Execute(null);
        await WaitForAsync(() => vm.RefreshCommand.CanExecute(null));

        Assert.Equal(["b", "a"], vm.Games.Select(row => row.Game.Id));
    }

    // --- Drop folder into Add Game ---------------------------------------------------

    [Fact]
    public async Task Dropping_a_folder_opens_add_game_with_that_path_and_does_not_guess_a_name()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();
        var folder = Path.Combine(_dir, "dropped-saves");
        Directory.CreateDirectory(folder);

        await vm.AddGameFromDroppedFolderAsync(folder);

        Assert.Equal(folder, Assert.Single(dialog.ShowAddGameInitialFolders));
        Assert.Empty(dialog.ErrorMessages);
    }

    [Fact]
    public async Task Toolbar_add_game_still_opens_the_dialog_without_a_prefilled_folder()
    {
        var (vm, dialog, _) = await CreateViewModelWithSavedCatalogAsync();

        vm.AddGameCommand.Execute(null);
        await WaitForAsync(() => dialog.ShowAddGameInitialFolders.Count == 1);

        Assert.Null(Assert.Single(dialog.ShowAddGameInitialFolders));
        Assert.Empty(dialog.ErrorMessages);
    }
}
