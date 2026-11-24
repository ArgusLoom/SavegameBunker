using SavegameBunker.Services;

namespace SavegameBunker.Tests;

/// <summary>
/// Exercises the lookup seam every view model and every XAML binding uses to reach an authored
/// sentence. Also the finding that decides the loading mechanism: if
/// <see cref="Strings"/> cannot load its dictionary from this plain xUnit host, nothing below can
/// even run.
/// </summary>
public sealed class StringsTests
{
    [Fact]
    public void A_known_key_returns_its_authored_sentence()
    {
        Assert.Equal("Add game", Strings.Get("str_toolbar_addGame"));
    }

    [Fact]
    public void An_unknown_key_throws_loudly_with_the_key_name_in_the_message()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Strings.Get("str_does_not_exist"));
        Assert.Contains("str_does_not_exist", ex.Message);
    }

    [Fact]
    public void Format_fills_a_composed_strings_placeholders()
    {
        Assert.Equal("2 hours ago", Strings.Format("str_relTime_hoursAgo", 2));
    }

    // --- Concurrency: the failure a manual walkthrough surfaced ----------------------
    //
    // StartStatusScan applies its results through Parallel.ForEachAsync, so GameRowViewModel's
    // Strings.Get calls run on four threads at once against a cold dictionary. A live
    // ResourceDictionary loaded from a Source URI defers its values as BAML and deserialises them
    // on first access, and that is not thread-safe: the running app produced both a null for a key
    // that plainly exists ("No string is authored for key 'str_game_noSaves'") and a torn read
    // ("Unexpected BAML record"). Strings materialises every entry up front for exactly this.

    [Fact]
    public void Concurrent_first_access_from_many_threads_resolves_every_key()
    {
        // Keys the status sweep actually touches from its worker threads.
        string[] keys =
        [
            "str_game_noSaves",
            "str_game_pathError",
            "str_common_checking",
            "str_status_upToDate",
            "str_status_neverBackedUp",
            "str_status_noSaveDataYet",
            "str_status_backupFolderUnavailable",
        ];

        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 200, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            try
            {
                foreach (var key in keys)
                {
                    var value = Strings.Get(key);
                    if (string.IsNullOrEmpty(value))
                        failures.Add($"{key} resolved empty on iteration {i}");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{ex.GetType().Name} on iteration {i}: {ex.Message}");
            }
        });

        Assert.Empty(failures);
    }

    [Fact]
    public void Preload_is_idempotent_and_safe_to_call_more_than_once()
    {
        Strings.Preload();
        Strings.Preload();

        Assert.Equal("No saves yet", Strings.Get("str_game_noSaves"));
    }

    // --- Plural agreement -----------------------------------------------------------------
    //
    // "Loaded 1 games." and "1 minutes ago" both shipped and were both caught by looking at the
    // running window rather than by a test. Every count-bearing sentence needs its own singular.

    [Theory]
    [InlineData("str_load_loadedOne", "str_load_loaded")]
    [InlineData("str_relTime_minuteAgo", "str_relTime_minutesAgo")]
    [InlineData("str_relTime_hourAgo", "str_relTime_hoursAgo")]
    [InlineData("str_relTime_dayAgo", "str_relTime_daysAgo")]
    [InlineData("str_restorePicker_headerOne", "str_restorePicker_headerMany")]
    // str_bulkBackup_one is deliberately absent: its singular ("Backed up {0}.") names the game
    // rather than stating a count, so the no-placeholder rule below does not apply to it.
    public void A_counted_sentence_has_both_a_singular_and_a_plural(string singularKey, string pluralKey)
    {
        var singular = Strings.Get(singularKey);
        var plural = Strings.Get(pluralKey);

        Assert.False(string.IsNullOrWhiteSpace(singular));
        Assert.False(string.IsNullOrWhiteSpace(plural));

        // The singular states its count outright, so it carries no {0} placeholder to fill.
        Assert.DoesNotContain("{0}", singular);
        Assert.NotEqual(singular, plural);
    }

    [Fact]
    public void The_singular_load_message_does_not_say_games()
    {
        Assert.Equal("Loaded 1 game.", Strings.Get("str_load_loadedOne"));
    }

    // --- Toolbar access keys ------------------------------------------
    //
    // Named explicitly rather than discovered by prefix, so a future eighth toolbar action
    // fails these tests until someone deliberately chooses its mnemonic letter.

    public static IEnumerable<object[]> ToolbarAccessKeyPairs =>
    [
        ["str_toolbar_addGame", "str_toolbar_addGame_access"],
        ["str_toolbar_findGames", "str_toolbar_findGames_access"],
        ["str_toolbar_refresh", "str_toolbar_refresh_access"],
        ["str_toolbar_openGamesList", "str_toolbar_openGamesList_access"],
        ["str_toolbar_changeBackupFolder", "str_toolbar_changeBackupFolder_access"],
        ["str_toolbar_backUpSelected", "str_toolbar_backUpSelected_access"],
        ["str_toolbar_restoreSelected", "str_toolbar_restoreSelected_access"],
    ];

    [Theory]
    [MemberData(nameof(ToolbarAccessKeyPairs))]
    public void Every_toolbar_access_label_is_its_clean_label_plus_one_underscore(string cleanKey, string accessKey)
    {
        var clean = Strings.Get(cleanKey);
        var access = Strings.Get(accessKey);

        Assert.Equal(1, access.Count(c => c == '_'));
        Assert.Equal(clean, access.Replace("_", string.Empty));
    }

    [Fact]
    public void The_eight_toolbar_mnemonics_are_all_different_letters()
    {
        // Two toolbar buttons sharing an access-key letter makes Alt+letter cycle focus between
        // the candidates instead of invoking either outright — the exact failure the letter
        // assignment exists to avoid, and the reason Restore selected takes a mid-word "T"
        // instead of colliding with Refresh's "R", and About takes "u" instead of colliding with
        // Back up's "B".
        string[] accessKeys =
        [
            "str_toolbar_addGame_access",
            "str_toolbar_findGames_access",
            "str_toolbar_refresh_access",
            "str_toolbar_openGamesList_access",
            "str_toolbar_changeBackupFolder_access",
            "str_toolbar_backUpSelected_access",
            "str_toolbar_restoreSelected_access",
            "str_toolbar_about_access",
        ];

        var mnemonics = accessKeys
            .Select(Strings.Get)
            .Select(value => char.ToUpperInvariant(value[value.IndexOf('_') + 1]))
            .ToList();

        Assert.Equal(mnemonics.Distinct().Count(), mnemonics.Count);
    }
}
