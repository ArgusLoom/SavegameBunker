using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// End-to-end round trip of the service half of the Add Game path: an absolute folder templated
/// with <see cref="PathResolver.ToTemplate"/>, an id derived with <see cref="IdSlug.Derive"/>,
/// saved and reloaded — pinning that a game added through the dialog survives a restart.
/// </summary>
public sealed class AddGameFlowTests : IDisposable
{
    private readonly string _dir;

    public AddGameFlowTests()
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

    [Fact]
    public async Task Added_game_survives_a_save_and_reload_with_its_template_and_backup_root_intact()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var absoluteSaveDir = Path.Combine(documents, "SavegameBunkerTestGame", "saves");
        var template = PathResolver.ToTemplate(absoluteSaveDir);
        var id = IdSlug.Derive("SavegameBunker Test Game");

        var catalogPath = Path.Combine(_dir, "games.json");
        await GameCatalog.LoadAsync(catalogPath); // seeds the file so SaveAsync has something to replace

        var catalog = new Catalog
        {
            BackupRoot = "SavegameBunkerBackups",
            Games =
            [
                new() { Id = id, DisplayName = "SavegameBunker Test Game", SaveDirTemplate = template },
            ],
        };

        var saveResult = await GameCatalog.SaveAsync(catalogPath, catalog);
        Assert.True(saveResult.Success);

        var reloaded = await GameCatalog.LoadAsync(catalogPath);

        Assert.Null(reloaded.FatalError);
        Assert.Empty(reloaded.Problems);
        var game = Assert.Single(reloaded.Catalog.Games);
        Assert.Equal(id, game.Id);
        Assert.StartsWith("%DOCUMENTS%/", game.SaveDirTemplate);
        Assert.Equal("SavegameBunkerBackups", reloaded.Catalog.BackupRoot);
    }
}
