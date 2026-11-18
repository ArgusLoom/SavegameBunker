using System.IO;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Exercises <see cref="KnownGamesScanner.Scan"/> against a real directory tree under
/// <see cref="Path.GetTempPath"/> — no real known-games table is read, because the development
/// machine is not assumed to hold game saves: this machine has no game saves, so only a synthetic fixture proves
/// anything here.
/// </summary>
public sealed class KnownGamesScannerTests : IDisposable
{
    private readonly string _dir;

    public KnownGamesScannerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SavegameBunkerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            TestFileSystem.RemoveDenyRules(_dir);
            TestFileSystem.RemoveJunctions(_dir);
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory must not fail the test run.
        }
    }

    private static KnownGame EntryFor(string id, string displayName, string template) =>
        new() { Id = id, DisplayName = displayName, Aliases = [], SaveDirTemplate = template };

    private string FixtureDir(string name)
    {
        var path = Path.Combine(_dir, name);
        Directory.CreateDirectory(path);
        return path.Replace('\\', '/');
    }

    private void WriteFile(string relativePath, int bytes)
    {
        var path = Path.Combine(_dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    private static ScanResult RunScan(IReadOnlyList<KnownGame> table) =>
        new KnownGamesScanner().Scan(table, []);

    [Fact]
    public void A_directory_holding_one_file_produces_exactly_one_hit_matching_the_fixture_tree()
    {
        WriteFile(Path.Combine("elden-ring", "save.dat"), 3);
        var template = Path.Combine(_dir, "elden-ring").Replace('\\', '/');
        var table = new[] { EntryFor("elden-ring", "Elden Ring", template) };

        var result = RunScan(table);

        var hit = Assert.Single(result.Hits);
        Assert.Equal(1, hit.FileCount);
        Assert.Equal(3, hit.TotalBytes);
        Assert.Equal(Path.GetFullPath(Path.Combine(_dir, "elden-ring")), hit.ResolvedPath);
    }

    [Fact]
    public void A_hits_game_carries_the_table_entrys_stored_id_unchanged()
    {
        WriteFile(Path.Combine("hades-2", "save.dat"), 1);
        var template = Path.Combine(_dir, "hades-2").Replace('\\', '/');
        var table = new[] { EntryFor("hades-2", "Hades II", template) };

        var result = RunScan(table);

        var hit = Assert.Single(result.Hits);
        Assert.Equal("hades-2", hit.Game.Id);
    }

    [Fact]
    public void Hits_are_sorted_by_display_name_regardless_of_the_tables_own_order()
    {
        WriteFile(Path.Combine("c", "save.dat"), 1);
        WriteFile(Path.Combine("b", "save.dat"), 1);
        WriteFile(Path.Combine("a", "save.dat"), 1);

        var table = new[]
        {
            EntryFor("charlie", "Charlie Game", FixtureDir("c")),
            EntryFor("bravo", "Bravo Game", FixtureDir("b")),
            EntryFor("alpha", "Alpha Game", FixtureDir("a")),
        };

        var result = RunScan(table);

        Assert.Equal(
            ["Alpha Game", "Bravo Game", "Charlie Game"],
            result.Hits.Select(hit => hit.Game.DisplayName));
    }

    [Fact]
    public void An_existing_directory_holding_no_file_anywhere_in_its_tree_yields_zero_hits()
    {
        var template = FixtureDir("empty");
        var table = new[] { EntryFor("empty-game", "Empty Game", template) };

        var result = RunScan(table);

        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_directory_holding_zero_files_but_one_empty_subdirectory_still_yields_zero_hits()
    {
        var template = FixtureDir("with-empty-subdir");
        Directory.CreateDirectory(Path.Combine(_dir, "with-empty-subdir", "sub"));
        var table = new[] { EntryFor("empty-sub-game", "Empty Sub Game", template) };

        var result = RunScan(table);

        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_template_pointing_at_a_directory_that_does_not_exist_yields_zero_hits()
    {
        var template = Path.Combine(_dir, "does-not-exist").Replace('\\', '/');
        var table = new[] { EntryFor("missing-game", "Missing Game", template) };

        var result = RunScan(table);

        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_template_with_an_unknown_variable_yields_zero_hits_and_does_not_abort_the_other_entries()
    {
        WriteFile(Path.Combine("good", "save.dat"), 1);

        var table = new[]
        {
            EntryFor("bad-game", "Bad Game", "%NOSUCHVAR%/x"),
            EntryFor("good-game", "Good Game", FixtureDir("good")),
        };

        var result = RunScan(table);

        var hit = Assert.Single(result.Hits);
        Assert.Equal("good-game", hit.Game.Id);
    }

    [Fact]
    public void A_table_entry_with_an_empty_id_is_skipped_even_though_its_directory_holds_files()
    {
        WriteFile(Path.Combine("blank-id", "save.dat"), 1);
        var table = new[] { EntryFor("", "Blank Id Game", FixtureDir("blank-id")) };

        var result = RunScan(table);

        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_table_entry_with_a_traversal_id_is_skipped_even_though_its_directory_holds_files()
    {
        WriteFile(Path.Combine("escape-id", "save.dat"), 1);
        var table = new[] { EntryFor("../escape", "Escape Game", FixtureDir("escape-id")) };

        var result = RunScan(table);

        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_table_entry_with_an_uppercase_and_spaced_id_is_skipped_for_the_same_reason()
    {
        WriteFile(Path.Combine("elden-ring-id", "save.dat"), 1);
        var table = new[] { EntryFor("Elden Ring", "Elden Ring", FixtureDir("elden-ring-id")) };

        var result = RunScan(table);

        Assert.Empty(result.Hits);
    }

    [Fact]
    public void A_table_with_two_independent_collision_groups_and_one_lone_entry_hits_only_the_lone_entry()
    {
        WriteFile(Path.Combine("shared-a", "save.dat"), 1);
        WriteFile(Path.Combine("shared-b", "save.dat"), 1);
        WriteFile(Path.Combine("lone", "save.dat"), 1);

        var sharedTemplateA = FixtureDir("shared-a");
        var sharedTemplateB = FixtureDir("shared-b");

        var table = new[]
        {
            EntryFor("shared-a-1", "Shared A One", sharedTemplateA),
            EntryFor("shared-a-2", "Shared A Two", sharedTemplateA),
            EntryFor("shared-b-1", "Shared B One", sharedTemplateB),
            EntryFor("shared-b-2", "Shared B Two", sharedTemplateB),
            EntryFor("shared-b-3", "Shared B Three", sharedTemplateB),
            EntryFor("lone-game", "Lone Game", FixtureDir("lone")),
        };

        var result = RunScan(table);

        var hit = Assert.Single(result.Hits);
        Assert.Equal("lone-game", hit.Game.Id);
        Assert.Equal(5, result.SkippedShared.Count);
    }

    [Fact]
    public void SkippedShared_comes_back_sorted_by_display_name_regardless_of_the_tables_own_order()
    {
        var sharedTemplate = FixtureDir("shared-order");

        var table = new[]
        {
            EntryFor("charlie", "Charlie Game", sharedTemplate),
            EntryFor("bravo", "Bravo Game", sharedTemplate),
            EntryFor("alpha", "Alpha Game", sharedTemplate),
        };

        var result = RunScan(table);

        Assert.Equal(
            ["Alpha Game", "Bravo Game", "Charlie Game"],
            result.SkippedShared.Select(entry => entry.DisplayName));
    }

    [Fact]
    public void Two_entries_whose_templates_differ_only_by_letter_case_are_both_treated_as_shared()
    {
        var template = FixtureDir("shared-case");

        var table = new[]
        {
            EntryFor("shared-case-1", "Shared Case One", template),
            EntryFor("shared-case-2", "Shared Case Two", template.ToUpperInvariant()),
        };

        var result = RunScan(table);

        Assert.Empty(result.Hits);
        Assert.Equal(2, result.SkippedShared.Count);
    }

    [Fact]
    public void A_shared_template_entry_whose_directory_does_not_exist_is_still_reported_as_skipped()
    {
        var template = Path.Combine(_dir, "shared-missing").Replace('\\', '/');

        var table = new[]
        {
            EntryFor("shared-missing-1", "Shared Missing One", template),
            EntryFor("shared-missing-2", "Shared Missing Two", template),
        };

        var result = RunScan(table);

        Assert.Empty(result.Hits);
        Assert.Equal(2, result.SkippedShared.Count);
    }

    private static Game GameFor(string id, string displayName, string template) =>
        new() { Id = id, DisplayName = displayName, SaveDirTemplate = template };

    [Fact]
    public void A_table_entry_whose_resolved_path_matches_a_catalog_entrys_resolved_path_is_already_known()
    {
        WriteFile(Path.Combine("already-known-path", "save.dat"), 1);
        var dirPath = Path.Combine(_dir, "already-known-path");

        var catalog = new[] { GameFor("existing-game", "Existing Game", dirPath.Replace('/', '\\')) };
        var table = new[] { EntryFor("new-id", "New Game", dirPath.Replace('\\', '/')) };

        var result = new KnownGamesScanner().Scan(table, catalog);

        Assert.Empty(result.Hits);
        Assert.Equal(1, result.SkippedAlreadyKnown);
    }

    [Fact]
    public void A_trailing_separator_on_either_side_does_not_prevent_the_already_known_match()
    {
        WriteFile(Path.Combine("trailing-sep", "save.dat"), 1);
        var dirPath = Path.Combine(_dir, "trailing-sep");

        var catalog = new[] { GameFor("existing-trailing", "Existing Trailing", dirPath.Replace('\\', '/') + "/") };
        var table = new[] { EntryFor("new-trailing", "New Trailing", dirPath.Replace('\\', '/')) };

        var result = new KnownGamesScanner().Scan(table, catalog);

        Assert.Empty(result.Hits);
        Assert.Equal(1, result.SkippedAlreadyKnown);
    }

    [Fact]
    public void A_table_entry_whose_id_matches_a_catalog_entrys_id_case_insensitively_is_already_known()
    {
        WriteFile(Path.Combine("elden-ring-dir", "save.dat"), 1);
        var template = FixtureDir("elden-ring-dir");

        var catalog = new[] { GameFor("Elden-Ring", "Elden Ring", FixtureDir("catalog-elden-ring")) };
        var table = new[] { EntryFor("elden-ring", "Elden Ring", template) };

        var result = new KnownGamesScanner().Scan(table, catalog);

        Assert.Empty(result.Hits);
        Assert.Equal(1, result.SkippedAlreadyKnown);
    }

    [Fact]
    public void An_entry_matching_by_both_path_and_id_counts_once_not_twice()
    {
        WriteFile(Path.Combine("both-match", "save.dat"), 1);
        var template = FixtureDir("both-match");

        var catalog = new[] { GameFor("both-match-game", "Both Match Game", template) };
        var table = new[] { EntryFor("both-match-game", "Both Match Game", template) };

        var result = new KnownGamesScanner().Scan(table, catalog);

        Assert.Empty(result.Hits);
        Assert.Equal(1, result.SkippedAlreadyKnown);
    }

    [Fact]
    public void A_catalog_entry_whose_own_template_does_not_resolve_does_not_suppress_unrelated_hits()
    {
        WriteFile(Path.Combine("unrelated-hit", "save.dat"), 1);
        var template = FixtureDir("unrelated-hit");

        var catalog = new[] { GameFor("broken-catalog-entry", "Broken", "%NOSUCHVAR%/x") };
        var table = new[] { EntryFor("unrelated-game", "Unrelated Game", template) };

        var result = new KnownGamesScanner().Scan(table, catalog);

        var hit = Assert.Single(result.Hits);
        Assert.Equal("unrelated-game", hit.Game.Id);
        Assert.Equal(0, result.SkippedAlreadyKnown);
    }

    [Fact]
    public void An_empty_catalog_suppresses_nothing()
    {
        WriteFile(Path.Combine("empty-catalog-hit", "save.dat"), 1);
        var template = FixtureDir("empty-catalog-hit");
        var table = new[] { EntryFor("empty-catalog-game", "Empty Catalog Game", template) };

        var result = new KnownGamesScanner().Scan(table, []);

        Assert.Single(result.Hits);
        Assert.Equal(0, result.SkippedAlreadyKnown);
    }

    [Fact]
    public void An_entry_that_is_both_shared_and_already_known_is_reported_only_as_shared()
    {
        var sharedTemplate = FixtureDir("shared-and-known");

        var catalog = new[] { GameFor("catalog-game", "Catalog Game", sharedTemplate) };
        var table = new[]
        {
            EntryFor("shared-1", "Shared One", sharedTemplate),
            EntryFor("shared-2", "Shared Two", sharedTemplate),
        };

        var result = new KnownGamesScanner().Scan(table, catalog);

        Assert.Equal(2, result.SkippedShared.Count);
        Assert.Equal(0, result.SkippedAlreadyKnown);
    }

    [Fact]
    public void Running_scan_twice_over_unchanged_inputs_returns_an_equal_result()
    {
        WriteFile(Path.Combine("idempotent-hit", "save.dat"), 1);
        var hitTemplate = FixtureDir("idempotent-hit");
        var sharedTemplate = FixtureDir("idempotent-shared");

        var catalog = new[] { GameFor("existing", "Existing", FixtureDir("idempotent-existing")) };
        var table = new[]
        {
            EntryFor("hit-game", "Hit Game", hitTemplate),
            EntryFor("shared-1", "Shared One", sharedTemplate),
            EntryFor("shared-2", "Shared Two", sharedTemplate),
            EntryFor("existing", "Existing Dup", FixtureDir("idempotent-existing-2")),
        };

        var scanner = new KnownGamesScanner();
        var first = scanner.Scan(table, catalog);
        var second = scanner.Scan(table, catalog);

        Assert.Equal(first.Hits.Select(h => h.Game.Id), second.Hits.Select(h => h.Game.Id));
        Assert.Equal(first.SkippedShared.Select(e => e.Id), second.SkippedShared.Select(e => e.Id));
        Assert.Equal(first.SkippedAlreadyKnown, second.SkippedAlreadyKnown);
    }
}
