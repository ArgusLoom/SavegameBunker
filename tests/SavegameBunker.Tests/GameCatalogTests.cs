using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Each test gets its own temporary directory, so the seeding and never-overwrite behaviour can
/// be exercised against real files rather than a mock filesystem.
/// </summary>
public sealed class GameCatalogTests : IDisposable
{
    private readonly TempDir _temp = new();
    private readonly string _dir;

    public GameCatalogTests()
    {
        _dir = _temp.DirectoryPath;
    }

    public void Dispose() => _temp.Dispose();

    private string Write(string content)
    {
        var path = Path.Combine(_dir, "games.json");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Loads_games_and_backup_root()
    {
        var path = Write("""
            {
              "backupRoot": "E:/SavegameBunkerBackups",
              "games": [
                { "id": "witcher3", "displayName": "The Witcher 3", "saveDirTemplate": "%DOCUMENTS%/W3" }
              ]
            }
            """);

        var load = await GameCatalog.LoadAsync(path);

        Assert.Null(load.FatalError);
        Assert.Empty(load.Problems);
        Assert.False(load.Seeded);
        Assert.Equal("E:/SavegameBunkerBackups", load.Catalog.BackupRoot);
        var game = Assert.Single(load.Catalog.Games);
        Assert.Equal("witcher3", game.Id);
    }

    [Fact]
    public async Task Comments_and_trailing_commas_are_accepted()
    {
        var path = Write("""
            // a comment
            {
              "games": [
                { "id": "a", "displayName": "A", "saveDirTemplate": "%APPDATA%/A" },
              ],
            }
            """);

        var load = await GameCatalog.LoadAsync(path);

        Assert.Null(load.FatalError);
        Assert.Single(load.Catalog.Games);
    }

    [Fact]
    public async Task Missing_file_is_seeded_and_the_result_says_so()
    {
        var path = Path.Combine(_dir, "absent.json");

        var load = await GameCatalog.LoadAsync(path);

        Assert.True(load.Seeded);
        Assert.Null(load.FatalError);
        Assert.Empty(load.Catalog.Games);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Seeded_template_loads_cleanly_on_the_next_start()
    {
        var path = Path.Combine(_dir, "absent.json");
        await GameCatalog.LoadAsync(path);

        var reloaded = await GameCatalog.LoadAsync(path);

        Assert.False(reloaded.Seeded);
        Assert.Null(reloaded.FatalError);
        Assert.Empty(reloaded.Problems);
        Assert.Empty(reloaded.Catalog.Games);
    }

    [Fact]
    public async Task Malformed_file_is_reported_and_left_byte_for_byte_alone()
    {
        const string broken = "{ this is not json";
        var path = Write(broken);

        var load = await GameCatalog.LoadAsync(path);

        Assert.NotNull(load.FatalError);
        Assert.Contains(path, load.FatalError);
        Assert.Empty(load.Catalog.Games);
        Assert.Equal(broken, File.ReadAllText(path));
    }

    [Fact]
    public async Task Games_null_is_reported_as_fatal_not_a_crash()
    {
        var path = Write("""{ "games": null }""");

        var load = await GameCatalog.LoadAsync(path);

        Assert.NotNull(load.FatalError);
        Assert.Empty(load.Catalog.Games);
        Assert.Equal("""{ "games": null }""", File.ReadAllText(path));
    }

    [Fact]
    public async Task A_null_entry_in_games_is_named_and_dropped_not_a_crash()
    {
        var path = Write("""
            {
              "games": [
                { "id": "good", "displayName": "Good", "saveDirTemplate": "%APPDATA%/Good" },
                null
              ]
            }
            """);

        var load = await GameCatalog.LoadAsync(path);

        Assert.Null(load.FatalError);
        Assert.Single(load.Catalog.Games);
        var problem = Assert.Single(load.Problems);
        Assert.Contains("entry 2", problem);
    }

    [Fact]
    public async Task Entry_with_a_missing_field_is_named_and_dropped()
    {
        var path = Write("""
            {
              "games": [
                { "id": "good", "displayName": "Good", "saveDirTemplate": "%APPDATA%/Good" },
                { "id": "nosaves", "displayName": "No Saves" }
              ]
            }
            """);

        var load = await GameCatalog.LoadAsync(path);

        Assert.Null(load.FatalError);
        Assert.Single(load.Catalog.Games);
        var problem = Assert.Single(load.Problems);
        Assert.Contains("No Saves", problem);
        Assert.Contains("saveDirTemplate", problem);
    }

    [Fact]
    public async Task Duplicate_id_is_reported_and_the_second_entry_dropped()
    {
        var path = Write("""
            {
              "games": [
                { "id": "dup", "displayName": "First", "saveDirTemplate": "%APPDATA%/A" },
                { "id": "dup", "displayName": "Second", "saveDirTemplate": "%APPDATA%/B" }
              ]
            }
            """);

        var load = await GameCatalog.LoadAsync(path);

        Assert.Single(load.Catalog.Games);
        Assert.Equal("First", load.Catalog.Games[0].DisplayName);
        Assert.Contains("duplicate id", Assert.Single(load.Problems));
    }

    [Theory]
    [InlineData("Witcher3")]
    [InlineData("../escape")]
    [InlineData("with space")]
    [InlineData("with_underscore")]
    public async Task Unsafe_id_is_rejected_because_it_names_a_backup_folder(string id)
    {
        var path = Write($$"""
            {
              "games": [
                { "id": "{{id}}", "displayName": "Bad", "saveDirTemplate": "%APPDATA%/Bad" }
              ]
            }
            """);

        var load = await GameCatalog.LoadAsync(path);

        Assert.Empty(load.Catalog.Games);
        Assert.Contains("may only contain", Assert.Single(load.Problems));
    }

    [Fact]
    public async Task Unresolvable_backup_root_is_reported_without_dropping_the_games()
    {
        var path = Write("""
            {
              "backupRoot": "%NOT_A_REAL_VARIABLE%/backups",
              "games": [
                { "id": "a", "displayName": "A", "saveDirTemplate": "%APPDATA%/A" }
              ]
            }
            """);

        var load = await GameCatalog.LoadAsync(path);

        Assert.Single(load.Catalog.Games);
        Assert.Contains("backupRoot", Assert.Single(load.Problems));
    }

    [Fact]
    public async Task Relative_backup_root_resolves_next_to_the_catalog_file()
    {
        var path = Write("""
            {
              "backupRoot": "SavegameBunkerBackups",
              "games": []
            }
            """);

        var load = await GameCatalog.LoadAsync(path);

        Assert.True(GameCatalog.TryResolveBackupRoot(load, out var resolved, out var error));
        Assert.Null(error);
        Assert.Equal(Path.Combine(_dir, "SavegameBunkerBackups"), resolved);
    }

    [Fact]
    public async Task Absent_backup_root_is_not_a_problem_but_does_not_resolve()
    {
        var path = Write("""{ "games": [] }""");

        var load = await GameCatalog.LoadAsync(path);

        Assert.Empty(load.Problems);
        Assert.Null(load.Catalog.BackupRoot);
        Assert.False(GameCatalog.TryResolveBackupRoot(load, out _, out _));
    }

    [Fact]
    public async Task SaveAsync_writes_games_ordered_by_display_name_case_insensitively()
    {
        var path = Write("""{ "games": [] }""");
        var catalog = new Catalog
        {
            BackupRoot = "E:/SavegameBunkerBackups",
            Games =
            [
                new() { Id = "b", DisplayName = "B", SaveDirTemplate = "%APPDATA%/B" },
                new() { Id = "a", DisplayName = "A", SaveDirTemplate = "%APPDATA%/A" },
            ],
        };

        var saveResult = await GameCatalog.SaveAsync(path, catalog);
        Assert.True(saveResult.Success);

        var reloaded = await GameCatalog.LoadAsync(path);

        Assert.Equal("E:/SavegameBunkerBackups", reloaded.Catalog.BackupRoot);
        Assert.Equal(["a", "b"], reloaded.Catalog.Games.Select(g => g.Id));
    }

    [Fact]
    public async Task SaveAsync_orders_a_lowercase_name_among_uppercase_ones_by_letter()
    {
        var path = Write("""{ "games": [] }""");
        var catalog = new Catalog
        {
            Games =
            [
                new() { Id = "zebra", DisplayName = "Zebra", SaveDirTemplate = "%APPDATA%/Zebra" },
                new() { Id = "apple", DisplayName = "apple", SaveDirTemplate = "%APPDATA%/apple" },
                new() { Id = "banana", DisplayName = "Banana", SaveDirTemplate = "%APPDATA%/Banana" },
            ],
        };

        Assert.True((await GameCatalog.SaveAsync(path, catalog)).Success);
        var reloaded = await GameCatalog.LoadAsync(path);

        Assert.Equal(["apple", "banana", "zebra"], reloaded.Catalog.Games.Select(g => g.Id));
    }

    [Fact]
    public async Task SaveAsync_keeps_equal_display_names_in_the_order_they_were_passed_in()
    {
        var path = Write("""{ "games": [] }""");
        var catalog = new Catalog
        {
            Games =
            [
                new() { Id = "first", DisplayName = "Same", SaveDirTemplate = "%APPDATA%/first" },
                new() { Id = "second", DisplayName = "Same", SaveDirTemplate = "%APPDATA%/second" },
            ],
        };

        Assert.True((await GameCatalog.SaveAsync(path, catalog)).Success);
        var reloaded = await GameCatalog.LoadAsync(path);

        Assert.Equal(["first", "second"], reloaded.Catalog.Games.Select(g => g.Id));
    }

    [Fact]
    public async Task SaveAsync_keeps_case_only_display_name_ties_in_their_relative_order()
    {
        var path = Write("""{ "games": [] }""");
        var catalog = new Catalog
        {
            Games =
            [
                new() { Id = "upper", DisplayName = "Doom", SaveDirTemplate = "%APPDATA%/upper" },
                new() { Id = "lower", DisplayName = "doom", SaveDirTemplate = "%APPDATA%/lower" },
            ],
        };

        Assert.True((await GameCatalog.SaveAsync(path, catalog)).Success);
        var reloaded = await GameCatalog.LoadAsync(path);

        Assert.Equal(["upper", "lower"], reloaded.Catalog.Games.Select(g => g.Id));
        Assert.Equal(2, reloaded.Catalog.Games.Count);
    }

    [Fact]
    public async Task SaveAsync_does_not_reorder_the_caller_s_games_list()
    {
        var path = Write("""{ "games": [] }""");
        var catalog = new Catalog
        {
            Games =
            [
                new() { Id = "b", DisplayName = "B", SaveDirTemplate = "%APPDATA%/B" },
                new() { Id = "a", DisplayName = "A", SaveDirTemplate = "%APPDATA%/A" },
            ],
        };

        Assert.True((await GameCatalog.SaveAsync(path, catalog)).Success);

        Assert.Equal(["b", "a"], catalog.Games.Select(g => g.Id));
    }

    [Fact]
    public async Task SaveAsync_round_trips_an_empty_catalog_and_a_single_game_without_error()
    {
        var emptyPath = Write("""{ "games": [] }""");
        Assert.True((await GameCatalog.SaveAsync(emptyPath, new Catalog { Games = [] })).Success);
        var emptyReload = await GameCatalog.LoadAsync(emptyPath);
        Assert.Empty(emptyReload.Catalog.Games);
        Assert.Null(emptyReload.FatalError);

        var onePath = Path.Combine(_dir, "one.json");
        File.WriteAllText(onePath, """{ "games": [] }""");
        var one = new Catalog
        {
            Games =
            [
                new() { Id = "only", DisplayName = "Only", SaveDirTemplate = "%APPDATA%/Only" },
            ],
        };
        Assert.True((await GameCatalog.SaveAsync(onePath, one)).Success);
        var oneReload = await GameCatalog.LoadAsync(onePath);
        Assert.Equal(["only"], oneReload.Catalog.Games.Select(g => g.Id));
        Assert.Null(oneReload.FatalError);
    }

    [Fact]
    public async Task LoadAsync_returns_an_unsorted_file_in_file_order_and_does_not_write()
    {
        var path = Write("""
            {
              "games": [
                { "id": "b", "displayName": "B", "saveDirTemplate": "%APPDATA%/B" },
                { "id": "a", "displayName": "A", "saveDirTemplate": "%APPDATA%/A" }
              ]
            }
            """);
        var before = File.GetLastWriteTimeUtc(path);

        var load = await GameCatalog.LoadAsync(path);

        Assert.Equal(["b", "a"], load.Catalog.Games.Select(g => g.Id));
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task SaveAsync_leaves_no_temp_file_behind_on_success()
    {
        var path = Write("""{ "games": [] }""");
        var catalog = new Catalog { Games = [] };

        var result = await GameCatalog.SaveAsync(path, catalog);

        Assert.True(result.Success);
        Assert.DoesNotContain(Directory.GetFiles(_dir), f => f.EndsWith(".tmp"));
    }

    [Fact]
    public async Task SaveAsync_with_a_stamp_refuses_when_the_file_changed_on_disk_since_it_was_loaded()
    {
        // Simulates a second SavegameBunker.exe pointed at the same catalog: this process's SaveGate
        // only serialises writers inside its own process, so nothing stops another process's write
        // landing between this process's load and its own save.
        var path = Write("""{ "games": [{ "id": "existing", "displayName": "Existing", "saveDirTemplate": "%APPDATA%/Existing" }] }""");
        var load = await GameCatalog.LoadAsync(path);
        Assert.NotNull(load.LastWriteTimeUtc);

        // A different process writes its own change to the same file.
        await Task.Delay(20); // NTFS last-write-time has ~10-20ms granularity on some volumes.
        await File.WriteAllTextAsync(path, """{ "games": [{ "id": "from-elsewhere", "displayName": "From Elsewhere", "saveDirTemplate": "%APPDATA%/Elsewhere" }] }""");

        var newGame = new Game { Id = "new", DisplayName = "New", SaveDirTemplate = "%APPDATA%/New" };
        var catalogToSave = new Catalog { Games = [.. load.Catalog.Games, newGame] };
        var result = await GameCatalog.SaveAsync(path, catalogToSave, load.LastWriteTimeUtc);

        Assert.False(result.Success);
        // The other process's write survives untouched — this save never happened.
        var stillOnDisk = await GameCatalog.LoadAsync(path);
        var game = Assert.Single(stillOnDisk.Catalog.Games);
        Assert.Equal("from-elsewhere", game.Id);
    }

    [Fact]
    public async Task SaveAsync_with_a_stamp_succeeds_when_nothing_changed_the_file_since_it_was_loaded()
    {
        var path = Write("""{ "games": [] }""");
        var load = await GameCatalog.LoadAsync(path);

        var catalog = new Catalog
        {
            Games = [new() { Id = "a", DisplayName = "A", SaveDirTemplate = "%APPDATA%/A" }],
        };
        var result = await GameCatalog.SaveAsync(path, catalog, load.LastWriteTimeUtc);

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task SaveAsync_with_a_null_stamp_never_checks_and_always_overwrites()
    {
        var path = Write("""{ "games": [] }""");
        await File.WriteAllTextAsync(path, """{ "games": [{ "id": "from-elsewhere", "displayName": "From Elsewhere", "saveDirTemplate": "%APPDATA%/Elsewhere" }] }""");

        var catalog = new Catalog { Games = [] };
        var result = await GameCatalog.SaveAsync(path, catalog, expectedLastWriteTimeUtc: null);

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task SaveAsync_to_a_missing_directory_fails_and_names_the_path()
    {
        var path = Path.Combine(_dir, "missing-subdir", "games.json");
        var catalog = new Catalog { Games = [] };

        var result = await GameCatalog.SaveAsync(path, catalog);

        Assert.False(result.Success);
        Assert.Contains(path, result.Message);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Saving_over_a_hand_edited_catalog_drops_comments_but_keeps_every_entry()
    {
        var path = Write("""
            // hand-edited
            {
              "games": [
                { "id": "a", "displayName": "A", "saveDirTemplate": "%APPDATA%/A" },
              ],
            }
            """);

        var load = await GameCatalog.LoadAsync(path);
        var saveResult = await GameCatalog.SaveAsync(path, load.Catalog);
        Assert.True(saveResult.Success);

        var rawText = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("//", rawText);

        var reloaded = await GameCatalog.LoadAsync(path);
        var game = Assert.Single(reloaded.Catalog.Games);
        Assert.Equal("a", game.Id);
    }

    [Fact]
    public async Task SaveAsync_writes_the_file_even_when_the_destination_was_deleted_externally()
    {
        var path = Write("""{ "games": [] }""");
        await GameCatalog.LoadAsync(path);
        File.Delete(path);

        var catalog = new Catalog
        {
            Games = [new() { Id = "a", DisplayName = "A", SaveDirTemplate = "%APPDATA%/A" }],
        };
        var result = await GameCatalog.SaveAsync(path, catalog);

        Assert.True(result.Success);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Failed_save_leaves_the_existing_catalog_byte_for_byte_unchanged()
    {
        var path = Write("""{ "games": [] }""");
        var originalBytes = await File.ReadAllBytesAsync(path);

        // Holding the destination open with no shared access makes File.Replace fail.
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var catalog = new Catalog
            {
                Games = [new() { Id = "a", DisplayName = "A", SaveDirTemplate = "%APPDATA%/A" }],
            };
            var result = await GameCatalog.SaveAsync(path, catalog);

            Assert.False(result.Success);
        }

        var afterBytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(originalBytes, afterBytes);
    }

    [Fact]
    public async Task Concurrent_saves_all_succeed_and_leave_one_readable_catalog()
    {
        var path = Write("""{ "games": [] }""");

        // "Add game" and "Change backup folder" can both reach SaveAsync once their dialogs close;
        // nothing upstream serialises them. With a shared temp name they raced over one staging
        // file and published each other's bytes.
        var saves = Enumerable.Range(0, 8).Select(i => GameCatalog.SaveAsync(path, new Catalog
        {
            BackupRoot = $"D:/Backups/{i}",
            Games =
            [
                new() { Id = $"game-{i}", DisplayName = $"Game {i}", SaveDirTemplate = $"%APPDATA%/Game{i}" },
            ],
        }));

        var results = await Task.WhenAll(saves);

        Assert.All(results, r => Assert.True(r.Success, r.Message));

        // Last writer wins, but the survivor must be one complete catalog — not a torn blend of
        // several — and no staging file may be left behind.
        var load = await GameCatalog.LoadAsync(path);
        Assert.Null(load.FatalError);
        Assert.Empty(load.Problems);
        var game = Assert.Single(load.Catalog.Games);
        Assert.Equal($"D:/Backups/{game.Id.Split('-')[1]}", load.Catalog.BackupRoot);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void Command_line_argument_wins_over_the_default_location()
    {
        var explicitPath = Path.Combine(_dir, "retro.json");

        Assert.Equal(explicitPath, GameCatalog.ResolvePath([explicitPath]));
        Assert.Equal(GameCatalog.DefaultPath, GameCatalog.ResolvePath([]));
        Assert.Equal(GameCatalog.DefaultPath, GameCatalog.ResolvePath(["   "]));
    }

    /// <summary>
    /// <c>src/SavegameBunker/games.json</c> is copied to the build/publish output (CLAUDE.md's "one
    /// catalog file" rule) and is therefore what every fresh build ships — not
    /// <see cref="GameCatalog.SeedTemplate"/>, which only ever runs when no file exists yet. The
    /// two used to drift: the checked-in file once carried a developer's own two real catalog
    /// entries, so a build from source (or a bare publish output with no <c>games.json</c> already
    /// beside it from a previous run) started with demo data instead of the promised empty
    /// list. Pinned byte-for-byte against the real seed template rather than just "loads with
    /// zero games", so a future hand-edit that adds an entry (commented out or not) fails loudly
    /// here instead of shipping.
    /// </summary>
    [Fact]
    public void Shipped_games_json_is_the_same_empty_seed_template_a_fresh_run_would_write()
    {
        var repoRoot = FindRepoRoot();
        var shippedPath = Path.Combine(repoRoot, "src", "SavegameBunker", "games.json");
        Assert.True(File.Exists(shippedPath), $"Expected to find {shippedPath}.");

        var shippedContent = File.ReadAllText(shippedPath).Replace("\r\n", "\n");
        var seedContent = GameCatalog.SeedTemplate.Replace("\r\n", "\n");
        Assert.Equal(seedContent, shippedContent);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SavegameBunker.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
