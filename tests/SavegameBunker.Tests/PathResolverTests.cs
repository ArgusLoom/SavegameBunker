using System.IO;
using SavegameBunker.Services;

namespace SavegameBunker.Tests;

public sealed class PathResolverTests
{
    [Fact]
    public void Resolves_documents_pseudo_variable()
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        Assert.True(PathResolver.TryResolve(@"%DOCUMENTS%\The Witcher 3", null, out var path, out var error));
        Assert.Null(error);
        Assert.Equal(Path.Combine(expected, "The Witcher 3"), path);
    }

    [Fact]
    public void Resolves_savedgames_pseudo_variable()
    {
        var expected = KnownFolders.SavedGames;
        Assert.NotNull(expected);

        Assert.True(PathResolver.TryResolve(@"%SAVEDGAMES%\MyGame", null, out var path, out var error));
        Assert.Null(error);
        Assert.Equal(Path.Combine(expected!, "MyGame"), path);
    }

    [Fact]
    public void Forward_and_back_slashes_resolve_identically_for_savedgames()
    {
        Assert.True(PathResolver.TryResolve("%SAVEDGAMES%/MyGame", null, out var withSlashes, out _));
        Assert.True(PathResolver.TryResolve(@"%SAVEDGAMES%\MyGame", null, out var withBackslashes, out _));

        Assert.Equal(withBackslashes, withSlashes);
    }

    [Fact]
    public void Savedgames_mixed_with_an_unknown_variable_is_still_reported_as_unresolved()
    {
        Assert.False(PathResolver.TryResolve(@"%SAVEDGAMES%\%NOT_A_REAL_VARIABLE%\saves", null, out var path, out var error));
        Assert.Equal("", path);
        Assert.Contains("unresolved variable", error);
    }

    [Fact]
    public void Resolves_standard_environment_variables()
    {
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.True(PathResolver.TryResolve(@"%APPDATA%\StardewValley\Saves", null, out var path, out _));
        Assert.Equal(Path.Combine(expected, "StardewValley", "Saves"), path);
    }

    [Fact]
    public void Forward_and_back_slashes_resolve_identically()
    {
        Assert.True(PathResolver.TryResolve("%APPDATA%/EldenRing/saves", null, out var withSlashes, out _));
        Assert.True(PathResolver.TryResolve(@"%APPDATA%\EldenRing\saves", null, out var withBackslashes, out _));

        Assert.Equal(withBackslashes, withSlashes);
    }

    [Fact]
    public void Unknown_variable_is_reported_instead_of_becoming_a_relative_path()
    {
        // The old implementation handed this to Path.GetFullPath, which produced an
        // absolute-looking path under the application directory.
        Assert.False(PathResolver.TryResolve(@"%NOT_A_REAL_VARIABLE%\saves", null, out var path, out var error));
        Assert.Equal("", path);
        Assert.Contains("unresolved variable", error);
    }

    [Fact]
    public void Process_environment_variables_outside_the_documented_five_are_not_expanded()
    {
        // Environment.ExpandEnvironmentVariables would silently expand %TEMP% (or any other
        // process env var) even though it is not part of the catalog's documented variable set.
        // %TEMP% is guaranteed to be set on every Windows machine, so this is deterministic.
        Assert.False(PathResolver.TryResolve(@"%TEMP%\saves", null, out var path, out var error));
        Assert.Equal("", path);
        Assert.Contains("unresolved variable", error);
        Assert.Contains("%TEMP%", error);
    }

    [Fact]
    public void A_literal_percent_sign_in_a_path_is_not_mistaken_for_an_unresolved_variable()
    {
        Assert.True(PathResolver.TryResolve(@"%APPDATA%\100% saves", null, out var path, out var error));
        Assert.Null(error);
        Assert.EndsWith(@"100% saves", path);
    }

    [Fact]
    public void Relative_template_without_a_base_directory_is_rejected()
    {
        Assert.False(PathResolver.TryResolve(@"some\relative\path", null, out _, out var error));
        Assert.Contains("not an absolute path", error);
    }

    [Fact]
    public void Relative_template_resolves_against_the_base_directory()
    {
        var baseDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        Assert.True(PathResolver.TryResolve("SavegameBunkerBackups", baseDir, out var path, out _));
        Assert.Equal(Path.Combine(baseDir, "SavegameBunkerBackups"), path);
    }

    [Fact]
    public void Absolute_template_ignores_the_base_directory()
    {
        Assert.True(PathResolver.TryResolve(@"E:/SavegameBunkerBackups", @"C:\somewhere\else", out var path, out _));
        Assert.Equal(@"E:\SavegameBunkerBackups", path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_template_is_rejected(string? template)
    {
        Assert.False(PathResolver.TryResolve(template, null, out _, out var error));
        Assert.Equal("path is empty", error);
    }

    [Fact]
    public void ToTemplate_folder_directly_under_documents_returns_the_documents_template()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var absolute = Path.Combine(documents, "The Witcher 3", "gamesaves");

        var template = PathResolver.ToTemplate(absolute);

        Assert.Equal("%DOCUMENTS%/The Witcher 3/gamesaves", template);
    }

    [Fact]
    public void ToTemplate_documents_root_itself_returns_the_bare_token_with_no_trailing_separator()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var template = PathResolver.ToTemplate(documents);

        Assert.Equal("%DOCUMENTS%", template);
    }

    [Fact]
    public void ToTemplate_folder_under_appdata_returns_the_appdata_template()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var absolute = Path.Combine(appData, "StardewValley", "Saves");

        var template = PathResolver.ToTemplate(absolute);

        Assert.Equal("%APPDATA%/StardewValley/Saves", template);
    }

    [Fact]
    public void ToTemplate_longest_matching_root_wins_when_two_candidates_both_match()
    {
        // %APPDATA% normally lives under %USERPROFILE%, so a folder directly under AppData must
        // template against the longer, more specific root rather than the profile root.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.StartsWith(userProfile, appData, StringComparison.OrdinalIgnoreCase);

        var template = PathResolver.ToTemplate(appData);

        Assert.Equal("%APPDATA%", template);
    }

    [Fact]
    public void ToTemplate_sibling_directory_sharing_a_prefix_is_not_templated_against_documents()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var lookalike = documents + "Backup";

        var template = PathResolver.ToTemplate(lookalike);

        // Must never wrongly match %DOCUMENTS% via a bare prefix check. It may legitimately match
        // a different candidate root (e.g. %USERPROFILE%, if Documents lives directly under it) —
        // what matters is that the result round-trips back to the exact original absolute path,
        // which a dropped directory-separator boundary check would corrupt.
        Assert.DoesNotContain("%DOCUMENTS%", template);
        Assert.True(PathResolver.TryResolve(template, null, out var resolved, out var error));
        Assert.Null(error);
        Assert.Equal(Path.GetFullPath(lookalike), resolved);
    }

    [Fact]
    public void ToTemplate_path_outside_every_known_root_returns_an_absolute_path_with_forward_slashes()
    {
        var outside = @"D:\SomeGame\saves";

        var template = PathResolver.ToTemplate(outside);

        Assert.Equal("D:/SomeGame/saves", template);
    }

    [Fact]
    public void ToTemplate_keeps_a_drive_root_a_root()
    {
        // TrimEnd(Path.DirectorySeparatorChar) turned E:\ into E:, which Windows reads as the
        // current directory on that drive rather than its root.
        var template = PathResolver.ToTemplate(@"E:\");

        Assert.Equal("E:/", template);
        Assert.True(PathResolver.TryResolve(template, null, out var resolved, out var error));
        Assert.Null(error);
        Assert.Equal(@"E:\", resolved);
    }

    [Fact]
    public void ToTemplate_output_round_trips_through_TryResolve()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var absolute = Path.Combine(documents, "RoundTripGame", "saves");

        var template = PathResolver.ToTemplate(absolute);

        Assert.True(PathResolver.TryResolve(template, null, out var resolved, out var error));
        Assert.Null(error);
        Assert.Equal(Path.GetFullPath(absolute), resolved);
    }

    [Fact]
    public void ToTemplate_folder_under_saved_games_returns_the_savedgames_template()
    {
        var savedGames = KnownFolders.SavedGames;
        Assert.NotNull(savedGames);
        var absolute = Path.Combine(savedGames!, "CD Projekt Red", "Cyberpunk 2077");

        var template = PathResolver.ToTemplate(absolute);

        Assert.Equal("%SAVEDGAMES%/CD Projekt Red/Cyberpunk 2077", template);
    }

    [Fact]
    public void ToTemplate_savedgames_root_itself_returns_the_bare_token_with_no_trailing_separator()
    {
        var savedGames = KnownFolders.SavedGames;
        Assert.NotNull(savedGames);

        var template = PathResolver.ToTemplate(savedGames!);

        Assert.Equal("%SAVEDGAMES%", template);
    }

    [Fact]
    public void ToTemplate_savedgames_wins_over_userprofile_when_both_match()
    {
        var savedGames = KnownFolders.SavedGames;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.NotNull(savedGames);
        Assert.StartsWith(userProfile, savedGames!, StringComparison.OrdinalIgnoreCase);

        var template = PathResolver.ToTemplate(savedGames!);

        Assert.Equal("%SAVEDGAMES%", template);
    }

    [Fact]
    public void ToTemplate_documents_wins_over_userprofile_when_both_match()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.StartsWith(userProfile, documents, StringComparison.OrdinalIgnoreCase);

        var template = PathResolver.ToTemplate(documents);

        Assert.Equal("%DOCUMENTS%", template);
    }

    [Fact]
    public void Savedgames_template_round_trips_through_TryResolve()
    {
        var savedGames = KnownFolders.SavedGames;
        Assert.NotNull(savedGames);
        var absolute = Path.Combine(savedGames!, "RoundTripGame", "saves");

        var template = PathResolver.ToTemplate(absolute);

        Assert.True(PathResolver.TryResolve(template, null, out var resolved, out var error));
        Assert.Null(error);
        Assert.Equal(Path.GetFullPath(absolute), resolved);
    }
}
