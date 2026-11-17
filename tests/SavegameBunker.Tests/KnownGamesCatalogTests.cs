using System.IO;
using System.Linq;
using System.Text;
using SavegameBunker.Models;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

public sealed class KnownGamesCatalogTests
{
    [Fact]
    public void All_has_at_least_fifty_entries()
    {
        Assert.True(KnownGamesCatalog.All.Count >= 50);
    }

    [Fact]
    public void Every_entry_has_a_non_blank_display_name_and_template()
    {
        foreach (var game in KnownGamesCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(game.DisplayName), "A shipped entry has a blank display name.");
            Assert.False(string.IsNullOrWhiteSpace(game.SaveDirTemplate), $"{game.DisplayName} has a blank save folder template.");
        }
    }

    [Fact]
    public void Every_template_resolves_without_an_unresolved_variable_error()
    {
        foreach (var game in KnownGamesCatalog.All)
        {
            var resolved = PathResolver.TryResolve(game.SaveDirTemplate, null, out _, out var error);
            Assert.True(resolved, $"{game.DisplayName}: \"{game.SaveDirTemplate}\" did not resolve — {error}");
        }
    }

    [Fact]
    public void Every_non_ascii_display_name_has_at_least_one_ascii_only_alias()
    {
        foreach (var game in KnownGamesCatalog.All)
        {
            if (game.DisplayName.All(IsAscii))
                continue;

            Assert.True(
                game.Aliases.Any(alias => alias.All(IsAscii)),
                $"{game.DisplayName} contains a non-ASCII character but has no ASCII-only alias.");
        }
    }

    private static bool IsAscii(char c) => c <= 127;

    [Fact]
    public void Find_matches_a_known_title_by_substring()
    {
        var results = KnownGamesCatalog.Find("witch", 8);

        Assert.Contains(results, g => g.DisplayName == "The Witcher 3: Wild Hunt");
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        var lower = KnownGamesCatalog.Find("witch", 8);
        var upper = KnownGamesCatalog.Find("WITCH", 8);

        Assert.Equal(lower.Select(g => g.DisplayName), upper.Select(g => g.DisplayName));
        Assert.Contains(upper, g => g.DisplayName == "The Witcher 3: Wild Hunt");
    }

    [Fact]
    public void An_entry_matched_only_through_an_alias_is_returned()
    {
        var results = KnownGamesCatalog.Find("tw3", 8);

        Assert.Contains(results, g => g.DisplayName == "The Witcher 3: Wild Hunt");
    }

    [Fact]
    public void TryExactMatch_hits_a_display_name()
    {
        Assert.True(KnownGamesCatalog.TryExactMatch("The Witcher 3: Wild Hunt", out var game));
        Assert.Equal("The Witcher 3: Wild Hunt", game!.DisplayName);
    }

    [Fact]
    public void TryExactMatch_hits_an_alias()
    {
        Assert.True(KnownGamesCatalog.TryExactMatch("Oblivion", out var game));
        Assert.Equal("The Elder Scrolls IV: Oblivion", game!.DisplayName);
    }

    [Fact]
    public void TryExactMatch_ignores_a_substring()
    {
        Assert.False(KnownGamesCatalog.TryExactMatch("witch", out var game));
        Assert.Null(game);
    }

    [Fact]
    public void Earlier_match_position_ranks_first_and_ties_break_alphabetically()
    {
        var results = KnownGamesCatalog.Find("war", 8).Select(g => g.DisplayName).ToList();

        Assert.Equal(
            new[]
            {
                "Total War: WARHAMMER II", // alias "Warhammer 2" matches at index 0
                "Hogwarts Legacy",
                "Star Wars Jedi: Fallen Order",
                "Star Wars Jedi: Survivor",
                "Total War: ROME II",
                "Total War: WARHAMMER III",
                "God of War",
                "Command & Conquer 3: Tiberium Wars",
            },
            results);
    }

    [Fact]
    public void Find_returns_at_most_max_entries_even_when_more_match()
    {
        var results = KnownGamesCatalog.Find("e", 8);

        Assert.Equal(8, results.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_query_returns_no_suggestions(string query)
    {
        Assert.Empty(KnownGamesCatalog.Find(query, 8));
    }

    [Fact]
    public void Query_matching_nothing_returns_an_empty_list_not_null()
    {
        var results = KnownGamesCatalog.Find("zzzznosuchgame", 8);

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public void Missing_embedded_resource_degrades_to_an_empty_list()
    {
        var result = KnownGamesCatalog.LoadFrom(null);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Malformed_resource_stream_degrades_to_an_empty_list_rather_than_throwing()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not valid json"));

        var result = KnownGamesCatalog.LoadFrom(stream);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Well_formed_stream_deserializes_into_known_games()
    {
        var json = """
            [
              { "displayName": "Test Game", "aliases": ["TG"], "saveDirTemplate": "%DOCUMENTS%/Test Game" }
            ]
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = KnownGamesCatalog.LoadFrom(stream);

        var game = Assert.Single(result);
        Assert.Equal("Test Game", game.DisplayName);
        Assert.Equal("%DOCUMENTS%/Test Game", game.SaveDirTemplate);
        Assert.Equal(new[] { "TG" }, game.Aliases);
    }

    [Fact]
    public void Every_entry_has_a_non_blank_id()
    {
        // Opened with NotEmpty: KnownGamesCatalog.Load degrades a corrupt embedded resource to an
        // empty list by design, and a per-entry loop over nothing would otherwise
        // assert nothing and pass vacuously.
        Assert.NotEmpty(KnownGamesCatalog.All);

        foreach (var game in KnownGamesCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(game.Id), $"{game.DisplayName} has a blank id.");
        }
    }

    [Fact]
    public void Every_id_is_safe_because_it_names_a_backup_folder()
    {
        Assert.NotEmpty(KnownGamesCatalog.All);

        foreach (var game in KnownGamesCatalog.All)
        {
            Assert.True(IdSlug.IsSafe(game.Id), $"{game.DisplayName}: \"{game.Id}\" is not a safe id.");

            // IdSlug.IsSafe is a shared rule a future requirement could widen (an underscore, a
            // dot). This shipped table's character set is a narrower promise than "whatever
            // IsSafe currently permits" — these ids name backup folders on machines this project
            // will never see, so the character set is asserted directly rather than only by
            // delegating to IsSafe.
            foreach (var c in game.Id)
            {
                var ok = c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '-';
                Assert.True(ok, $"{game.DisplayName}: \"{game.Id}\" contains a character outside a-z0-9-.");
            }
        }
    }

    [Fact]
    public void Ids_are_unique_across_the_table_ignoring_case()
    {
        Assert.NotEmpty(KnownGamesCatalog.All);

        var collisions = KnownGamesCatalog.All
            .GroupBy(game => game.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => string.Join(", ", group.Select(g => g.DisplayName)))
            .ToList();

        Assert.True(collisions.Count == 0, $"Colliding ids: {string.Join(" | ", collisions)}");
    }
}
