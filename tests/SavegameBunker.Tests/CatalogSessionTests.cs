using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;
using SavegameBunker.ViewModels;

namespace SavegameBunker.Tests;

/// <summary>
/// Proves the write-blocking half of the fatal-reload fix: once <see cref="CatalogSession.ReloadAsync"/>
/// has picked up a fatal error, every write goes through this one gate rather than each write method
/// separately re-deriving "is it safe to save right now".
/// </summary>
public sealed class CatalogSessionTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly string _dir;

    public CatalogSessionTests()
    {
        _dir = _temp.DirectoryPath;
    }

    public void Dispose() => _temp.Dispose();

    private string Write(string json)
    {
        var path = Path.Combine(_dir, "games.json");
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>
    /// Two in-app writes started back to back (say Find Games' add while a removal's save is still
    /// in flight) must both land: the session serializes them, so the second saves with the stamp
    /// the first one left instead of being refused as "changed on disk".
    /// </summary>
    [Fact]
    public async Task Overlapping_writes_from_one_session_both_succeed()
    {
        var path = Write("""
            {
              "games": [
                { "id": "existing", "displayName": "Existing", "saveDirTemplate": "%APPDATA%/Existing" }
              ]
            }
            """);
        var session = new CatalogSession(await GameCatalog.LoadAsync(path));
        var existing = session.Games[0];

        var add = session.AddGamesAsync([new Game { Id = "added", DisplayName = "Added", SaveDirTemplate = "%APPDATA%/Added" }]);
        var remove = session.RemoveGameAsync(existing);
        var results = await Task.WhenAll(add, remove);

        Assert.All(results, r => Assert.True(r.Success, r.Message));
        var reloaded = await GameCatalog.LoadAsync(path);
        Assert.Equal("added", Assert.Single(reloaded.Catalog.Games).Id);
    }

    [Fact]
    public async Task Reload_of_a_malformed_file_keeps_the_previously_loaded_catalog()
    {
        var path = Write("""
            {
              "games": [
                { "id": "existing", "displayName": "Existing", "saveDirTemplate": "%APPDATA%/Existing" }
              ]
            }
            """);
        var load = await GameCatalog.LoadAsync(path);
        Assert.Null(load.FatalError);
        var session = new CatalogSession(load);

        File.WriteAllText(path, "{ this is not json");
        await session.ReloadAsync();

        Assert.NotNull(session.FatalError);
        // The in-memory catalog still holds the entry from before the broken reload — not the
        // empty catalog GameCatalog.LoadAsync returns for a fatal load.
        Assert.Single(session.Games);
        Assert.Equal("existing", session.Games[0].Id);
    }

    [Fact]
    public async Task Add_games_is_refused_when_another_process_wrote_the_file_since_this_session_loaded_it()
    {
        // Simulates a second SavegameBunker.exe pointed at the same catalog, writing between this
        // session's load and its own write — CatalogSession.SaveGate only serialises writers
        // inside this one process.
        var path = Write("""
            {
              "games": [
                { "id": "existing", "displayName": "Existing", "saveDirTemplate": "%APPDATA%/Existing" }
              ]
            }
            """);
        var load = await GameCatalog.LoadAsync(path);
        var session = new CatalogSession(load);

        await Task.Delay(20); // NTFS last-write-time has ~10-20ms granularity on some volumes.
        File.WriteAllText(path, """
            {
              "games": [
                { "id": "from-another-process", "displayName": "From Another Process", "saveDirTemplate": "%APPDATA%/Elsewhere" }
              ]
            }
            """);

        var newGame = new Game { Id = "new", DisplayName = "New", SaveDirTemplate = "%APPDATA%/New" };
        var result = await session.AddGamesAsync([newGame]);

        Assert.False(result.Success);
        // The other process's write survives on disk untouched.
        var stillOnDisk = await GameCatalog.LoadAsync(path);
        var game = Assert.Single(stillOnDisk.Catalog.Games);
        Assert.Equal("from-another-process", game.Id);
    }

    [Fact]
    public async Task A_successful_write_updates_this_sessions_own_stamp_so_its_next_write_does_not_self_refuse()
    {
        var path = Write("""{ "games": [] }""");
        var load = await GameCatalog.LoadAsync(path);
        var session = new CatalogSession(load);

        var first = new Game { Id = "first", DisplayName = "First", SaveDirTemplate = "%APPDATA%/First" };
        var firstResult = await session.AddGamesAsync([first]);
        Assert.True(firstResult.Success, firstResult.Message);

        var second = new Game { Id = "second", DisplayName = "Second", SaveDirTemplate = "%APPDATA%/Second" };
        var secondResult = await session.AddGamesAsync([second]);

        Assert.True(secondResult.Success, secondResult.Message);
        Assert.Equal(2, session.Games.Count);
    }

    [Fact]
    public async Task Add_games_is_refused_after_a_fatal_reload_instead_of_overwriting_the_file_with_stale_data()
    {
        var path = Write("""
            {
              "games": [
                { "id": "existing", "displayName": "Existing", "saveDirTemplate": "%APPDATA%/Existing" }
              ]
            }
            """);
        var load = await GameCatalog.LoadAsync(path);
        var session = new CatalogSession(load);

        File.WriteAllText(path, "{ this is not json");
        await session.ReloadAsync();
        Assert.NotNull(session.FatalError);

        var newGame = new Game { Id = "new", DisplayName = "New", SaveDirTemplate = "%APPDATA%/New" };
        var result = await session.AddGamesAsync([newGame]);

        Assert.False(result.Success);
        // The broken file on disk is exactly what the user can hand-fix — a write here would have
        // clobbered it with a catalog built from stale in-memory data.
        Assert.Equal("{ this is not json", File.ReadAllText(path));
    }

    [Fact]
    public async Task Change_backup_root_is_refused_after_a_fatal_reload()
    {
        var path = Write("""{ "games": [] }""");
        var load = await GameCatalog.LoadAsync(path);
        var session = new CatalogSession(load);

        File.WriteAllText(path, "{ this is not json");
        await session.ReloadAsync();

        var result = await session.SetBackupRootAsync(_dir);

        Assert.False(result.Success);
        Assert.Equal("{ this is not json", File.ReadAllText(path));
    }

    [Fact]
    public async Task Change_save_folder_is_refused_after_a_fatal_reload()
    {
        var path = Write("""
            {
              "games": [
                { "id": "existing", "displayName": "Existing", "saveDirTemplate": "%APPDATA%/Existing" }
              ]
            }
            """);
        var load = await GameCatalog.LoadAsync(path);
        var session = new CatalogSession(load);
        var game = session.Games[0];

        File.WriteAllText(path, "{ this is not json");
        await session.ReloadAsync();

        var result = await session.SetSaveFolderAsync(game, "%APPDATA%/Elsewhere");

        Assert.False(result.Success);
        Assert.Equal("{ this is not json", File.ReadAllText(path));
    }
}
